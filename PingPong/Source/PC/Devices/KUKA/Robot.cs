using PingPong.Maths;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.KUKA {

    public class Robot : IDevice {

        private readonly object receivedDataSyncLock = new object();

        private readonly object forceMoveSyncLock = new object();

        private readonly object cancellationSyncLock = new object();

        private readonly object generatorSyncLock = new object();

        private readonly RSIAdapter rsiAdapter;

        private readonly TrajectoryGenerator generator;

        private CancellationTokenSource cts;

        private RobotVector position;

        private RobotAxisVector axisPosition;

        private RobotConfig config;

        private bool isInitialized = false; // lock ??

        private bool isForceMoveModeEnabled = false;

        private bool isCancellationRequested = false;

        private bool IsCancellationRequested {
            get {
                lock (cancellationSyncLock) {
                    return isCancellationRequested;
                }
            }
            set {
                lock (cancellationSyncLock) {
                    isCancellationRequested = value;
                }
            }
        }

        #region Properties

        public RobotConfig Config {
            get {
                return config;
            }
            set {
                if (isInitialized) {
                    throw new InvalidOperationException("Robot is already initialized");
                } else {
                    config = value;
                }
            }
        }

        public string Ip {
            get {
                return rsiAdapter.Ip;
            }
        }

        public int Port { 
            get {
                if (config != null) {
                    return config.Port;
                } else {
                    return 0;
                }
            } 
        }

        public RobotLimits Limits { 
            get {
                if (config != null) {
                    return config.Limits;
                } else {
                    return null;
                }
            }
        }

        public RobotVector HomePosition { get; private set; }

        public RobotVector Position {
            get {
                lock (receivedDataSyncLock) {
                    return position;
                }
            }
        }

        public RobotAxisVector AxisPosition {
            get {
                lock (receivedDataSyncLock) {
                    return axisPosition;
                }
            }
        }

        public RobotVector TheoreticalPosition {
            get {
                lock (generatorSyncLock) {
                    return generator.Position;
                }
            }
        }

        public RobotVector Velocity {
            get {
                lock (generatorSyncLock) {
                    return generator.Velocity;
                }
            }
        }

        public RobotVector Acceleration {
            get {
                lock (generatorSyncLock) {
                    return generator.Acceleration;
                }
            }
        }

        public RobotVector Jerk {
            get {
                lock (generatorSyncLock) {
                    return generator.Jerk;
                }
            }
        }

        public RobotVector TargetPosition {
            get {
                lock (generatorSyncLock) {
                    return generator.TargetPosition;
                }
            }
        }

        public bool IsTargetPositionReached {
            get {
                lock (generatorSyncLock) {
                    return generator.IsTargetPositionReached;
                }
            }
        }

        public Transformation OptiTrackTransformation {
            get {
                if (config != null) {
                    return config.Transformation;
                } else {
                    return null;
                }
            }
        }

        #endregion

        #region Events

        public event EventHandler Initialized;

        public event EventHandler Uninitialized;

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        public event EventHandler<FrameSentEventArgs> FrameSent;

        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured;

        public event EventHandler<MovementChangedEventArgs> MovementChanged;

        #endregion

        public Robot() {
            rsiAdapter = new RSIAdapter();
            generator = new TrajectoryGenerator();
            position = RobotVector.Zero;
            axisPosition = RobotAxisVector.Zero;
            HomePosition = RobotVector.Zero;
        }

        public Robot(RobotConfig config) : this() {
            this.config = config;
        }

        #region Communication methods

        private async Task Connect() {
            cts = new CancellationTokenSource();
            IsCancellationRequested = false;
            InputFrame receivedFrame = null;
            isInitialized = true;

            // Connect with the robot
            try {
                Task.Run(async () => {
                    receivedFrame = await rsiAdapter.Connect(Config.Port);
                }).Wait(cts.Token);
            } catch (OperationCanceledException) {
                rsiAdapter.Disconnect();
                return;
            }

            lock (generatorSyncLock) {
                generator.Initialize(receivedFrame.Position);
            }

            lock (receivedDataSyncLock) {
                position = receivedFrame.Position;
                HomePosition = receivedFrame.Position;
            }

            // Send first response
            OutputFrame response = new OutputFrame() {
                Correction = RobotVector.Zero,
                IPOC = receivedFrame.IPOC
            };

            rsiAdapter.SendData(response);
            Initialized?.Invoke(this, EventArgs.Empty);

            // Start loop for receiving and sending data
            while (!IsCancellationRequested) {
                long IPOC = await ReceiveDataAsync();
                SendData(IPOC);
            }
        }

        private async Task<long> ReceiveDataAsync() {
            InputFrame receivedFrame = await rsiAdapter.ReceiveDataAsync();

            if (!Limits.CheckAxisPosition(receivedFrame.AxisPosition)) {
                Uninitialize();
                throw new InvalidOperationException("Axis position limit has been exceeded:" +
                    $"{Environment.NewLine}{receivedFrame.AxisPosition}");
            }

            if (!Limits.CheckPosition(receivedFrame.Position)) {
                Uninitialize();
                throw new InvalidOperationException("Available workspace limit has been exceeded:" +
                    $"{Environment.NewLine}{receivedFrame.Position}");
            }

            lock (receivedDataSyncLock) {
                position = receivedFrame.Position;
                axisPosition = receivedFrame.AxisPosition;
            }

            FrameReceived?.Invoke(this, new FrameReceivedEventArgs {
                ReceivedFrame = receivedFrame
            });

            return receivedFrame.IPOC;
        }

        private void SendData(long IPOC) {
            RobotVector correction;
            RobotVector currentVelocity;
            RobotVector currentAcceleration;
            RobotVector targetPosition;
            RobotVector targetVelocity;
            double targetDuration;

            lock (generatorSyncLock) {
                // GetNextCorrection() updates theoretical values
                correction = generator.GetNextCorrection();
                currentVelocity = generator.Velocity;
                currentAcceleration = generator.Acceleration;
                targetPosition = generator.TargetPosition;
                targetVelocity = generator.TargetVelocity;
                targetDuration = generator.TargetDuration;
            }

            if (!Limits.CheckCorrection(correction)) {
                throw new InvalidOperationException("Correction limit has been exceeded:" +
                    $"{Environment.NewLine}{correction}");
            }

            if (!Limits.CheckVelocity(currentVelocity)) {
                throw new InvalidOperationException("Velocity limit has been exceeded:" +
                    $"{Environment.NewLine}{currentVelocity}");
            }

            if (!Limits.CheckAcceleration(currentAcceleration)) {
                throw new InvalidOperationException("Acceleration limit has been exceeded:" +
                    $"{Environment.NewLine}{currentAcceleration}");
            }

            OutputFrame outputFrame = new OutputFrame() {
                Correction = correction,
                IPOC = IPOC
            };

            rsiAdapter.SendData(outputFrame);

            FrameSent?.Invoke(this, new FrameSentEventArgs {
                FrameSent = outputFrame,
                Position = position,
                TargetPosition = targetPosition,
                TargetVelocity = targetVelocity,
                TargetDuration = targetDuration
            });
        }

        #endregion

        #region Movement methods

        public void MoveTo(RobotMovement[] movementsStack) {
            lock (forceMoveSyncLock) {
                if (isForceMoveModeEnabled) {
                    return;
                }
            }

            if (!isInitialized) {
                throw new InvalidOperationException("Robot is not initialized");
            }

            for (int i = 0; i < movementsStack.Length; i++) {
                RobotMovement movement = movementsStack[i];

                if (!Limits.CheckPosition(movement.TargetPosition)) {
                    throw new ArgumentException("Target position is outside the available workspace:" +
                        $"{Environment.NewLine}{movement.TargetPosition}");
                }

                if (!Limits.CheckVelocity(movement.TargetVelocity)) {
                    throw new ArgumentException("Target velocity exceeding max value:" +
                        $"{Environment.NewLine}{movement.TargetVelocity}");
                }
            }

            RobotVector currentVelocity;
            RobotVector currentAcceleration;

            lock (generatorSyncLock) {
                generator.SetMovements(movementsStack);
                currentVelocity = generator.Velocity;
                currentAcceleration = generator.Acceleration;
            }

            MovementChanged?.Invoke(this, new MovementChangedEventArgs {
                Position = Position,
                Velocity = currentVelocity,
                Acceleration = currentAcceleration,
                MovementsStack = movementsStack
            });
        }

        public void MoveTo(RobotMovement movement) {
            MoveTo(new RobotMovement[] { movement });
        }

        public void MoveTo(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            MoveTo(new RobotMovement(targetPosition, targetVelocity, targetDuration));
        }

        public void ForceMoveTo(RobotMovement[] movementsStack) {
            MoveTo(movementsStack);

            lock (forceMoveSyncLock) {
                isForceMoveModeEnabled = true;
            }

            ManualResetEvent targetPositionReached = new ManualResetEvent(false);

            void processFrame(object sender, FrameReceivedEventArgs args) {
                if (IsTargetPositionReached) {
                    FrameReceived -= processFrame;
                    targetPositionReached.Set();
                }
            };

            FrameReceived += processFrame;
            targetPositionReached.WaitOne();

            lock (forceMoveSyncLock) {
                isForceMoveModeEnabled = false;
            }
        }

        public void ForceMoveTo(RobotMovement movement) {
            ForceMoveTo(new RobotMovement[] { movement });
        }

        public void ForceMoveTo(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            ForceMoveTo(new RobotMovement(targetPosition, targetVelocity, targetDuration));
        }

        public void Shift(RobotVector deltaPosition, RobotVector targetVelocity, double targetDuration) {
            MoveTo(Position + deltaPosition, targetVelocity, targetDuration);
        }

        public void ForceShift(RobotVector deltaPosition, RobotVector targetVelocity, double targetDuration) {
            ForceMoveTo(Position + deltaPosition, targetVelocity, targetDuration);
        }

        #endregion

        public void Initialize() {
            if (isInitialized) {
                return;
            }

            if (config == null) {
                throw new InvalidOperationException("Robot configuration is not set.");
            }

            Task.Run(() => {
                Task communicationTask = Connect();

                communicationTask.ContinueWith(task => {
                    string robotAdress = ToString();
                    rsiAdapter.Disconnect();

                    if (task.IsFaulted) {
                        var args = new ErrorOccuredEventArgs {
                            RobotIp = robotAdress,
                            Exception = task.Exception.GetBaseException()
                        };

                        ErrorOccured?.Invoke(this, args);
                    }

                    isInitialized = false;
                    Uninitialized?.Invoke(this, EventArgs.Empty);
                });
            });
        }

        public void Uninitialize() {
            if (!isInitialized) {
                return;
            }

            if (cts != null) {
                cts.Cancel();
            }

            IsCancellationRequested = true;
        }

        public bool IsInitialized() {
            return isInitialized;
        }

        public override string ToString() {
            if (Ip != null) {
                return $"{Ip}:{Port}";
            } else {
                return $"0.0.0.0:0000";
            }
        }

    }
}