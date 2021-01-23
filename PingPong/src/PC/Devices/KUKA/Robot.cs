using PingPong.Maths;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.KUKA {

    public class Robot : IDevice {

        private readonly object receivedDataSyncLock = new object();

        private readonly object forceMoveSyncLock = new object();

        private readonly object cancellationSyncLock = new object();

        private readonly RSIAdapter rsiAdapter;

        private readonly TrajectoryGenerator5T generator;

        private CancellationTokenSource cts;

        private RobotVector position;

        private RobotAxisPosition axisPosition;

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

        /// <summary>
        /// Robot config
        /// </summary>
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

        /// <summary>
        /// Robot Ip adress (Robot Sensor Interface - RSI)
        /// </summary>
        public string Ip {
            get {
                return rsiAdapter.Ip;
            }
        }

        /// <summary>
        /// Port number (Robot Sensor Interface - RSI)
        /// </summary>
        public int Port { 
            get {
                if (config != null) {
                    return config.Port;
                } else {
                    return 0;
                }
            } 
        }

        /// <summary>
        /// Robot limits
        /// </summary>
        public RobotLimits Limits { 
            get {
                if (config != null) {
                    return config.Limits;
                } else {
                    return null;
                }
            }
        }

        /// <summary>
        /// Robot home position
        /// </summary>
        public RobotVector HomePosition { get; private set; }

        /// <summary>
        /// Robot actual position
        /// </summary>
        public RobotVector Position {
            get {
                lock (receivedDataSyncLock) {
                    return position;
                }
            }
        }

        /// <summary>
        /// Robot actual axis position
        /// </summary>
        public RobotAxisPosition AxisPosition {
            get {
                lock (receivedDataSyncLock) {
                    return axisPosition;
                }
            }
        }

        /// <summary>
        /// Robot (theoretical) actual position
        /// </summary>
        public RobotVector TheoreticalPosition {
            get {
                return generator.Position;
            }
        }

        /// <summary>
        /// Robot (theoretical) actual velocity
        /// </summary>
        public RobotVector Velocity {
            get {
                return generator.Velocity;
            }
        }

        /// <summary>
        /// Robot (theoretical) actual acceleration
        /// </summary>
        public RobotVector Acceleration {
            get {
                return generator.Acceleration;
            }
        }

        /// <summary>
        /// Robot actual target position
        /// </summary>
        public RobotVector TargetPosition {
            get {
                return generator.TargetPosition;
            }
        }

        /// <summary>
        /// Flag that indicates if robot reached target position
        /// </summary>
        public bool IsTargetPositionReached {
            get {
                return generator.IsTargetPositionReached;
            }
        }

        /// <summary>
        /// Transformation from OptiTrack coordinate system to this robot coordinate system
        /// </summary>
        public Transformation OptiTrackTransformation {
            get {
                if (config != null) {
                    return config.Transformation;
                } else {
                    return null;
                }
            }
        }
        
        /// <summary>
        /// Occurs when the robot is initialized (connection has been established)
        /// </summary>
        public event EventHandler Initialized;

        /// <summary>
        /// TODO
        /// </summary>
        public event EventHandler Uninitialized;

        /// <summary>
        /// Occurs when frame is received
        /// </summary>
        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        /// <summary>
        /// Occurs when frame is sent
        /// </summary>
        public event EventHandler<FrameSentEventArgs> FrameSent;

        /// <summary>
        /// Occurs when exception was thrown on robot thread, while receiving or sending data
        /// </summary>
        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured; 

        public Robot() {
            rsiAdapter = new RSIAdapter();
            generator = new TrajectoryGenerator5T();
            position = RobotVector.Zero;
            axisPosition = RobotAxisPosition.Zero;
        }

        public Robot(RobotConfig config) : this() {
            Config = config;
        }

        private async Task Connect() {
            cts = new CancellationTokenSource();
            IsCancellationRequested = false;
            InputFrame receivedFrame = null;

            throw new ArgumentException("abc");

            // Connect with the robot
            try {
                Task.Run(async () => {
                    receivedFrame = await rsiAdapter.Connect(Config.Port);
                }).Wait(cts.Token);
            } catch (OperationCanceledException) {
                rsiAdapter.Disconnect();
                return;
            }

            generator.Initialize(receivedFrame.Position);

            lock (receivedDataSyncLock) {
                position = receivedFrame.Position;
                HomePosition = receivedFrame.Position;
            }

            // Send first response
            OutputFrame response = new OutputFrame() {
                Correction = RobotVector.Zero,
                IPOC = receivedFrame.IPOC
            };

            isInitialized = true;
            Initialized?.Invoke(this, EventArgs.Empty);

            // Start loop for receiving and sending data
            while (!IsCancellationRequested) {
                long IPOC = await ReceiveDataAsync();
                SendData(IPOC);
            }
        }

        /// <summary>
        /// Receives data (IPOC, cartesian and axis position) from the robot asynchronously, 
        /// raises <see cref="Robot.FrameRecived">FrameReceived</see> event
        /// </summary>
        /// <returns>current IPOC timestamp</returns>
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

            var args = new FrameReceivedEventArgs {
                ReceivedFrame = receivedFrame,
                ActualPosition = receivedFrame.Position,
                TargetPosition = generator.TargetPosition,
                GenPosition = generator.Position,
                GenVelocity = generator.Velocity,
                GenAcceleration = generator.Acceleration,
                IsTargetPositionReached = generator.IsTargetPositionReached
            };

            FrameReceived?.Invoke(this, args);

            return receivedFrame.IPOC;
        }

        /// <summary>
        /// Sends data (IPOC, correction) to the robot, raises <see cref="Robot.FrameSent">FrameSent</see> event
        /// </summary>
        private void SendData(long IPOC) {
            RobotVector correction = generator.GetNextCorrection();

            if (!Limits.CheckRelativeCorrection(correction)) {
                Uninitialize();
                throw new InvalidOperationException("Correction limit has been exceeded:" +
                    $"{Environment.NewLine}{correction}");
            }

            OutputFrame outputFrame = new OutputFrame() {
                Correction = correction,
                IPOC = IPOC
            };

            rsiAdapter.SendData(outputFrame);

            var args = new FrameSentEventArgs {
                FrameSent = outputFrame,
                ActualPosition = position,
                TargetPosition = generator.TargetPosition,
                GenPosition = generator.Position,
                GenVelocity = generator.Velocity,
                GenAcceleration = generator.Acceleration
            };

            FrameSent?.Invoke(this, args);
        }

        /// <summary>
        /// Moves robot to specified position (sets target position).
        /// </summary>
        /// <param name="targetPosition">target position</param>
        /// <param name="targetVelocity">target velocity (velocity after targetDuration)</param>
        /// <param name="targetDuration">desired movement duration in seconds</param>
        public void MoveTo(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            if (!isInitialized) {
                throw new InvalidOperationException("Robot is not initialized");
            }

            if (!Limits.CheckPosition(targetPosition)) {
                throw new ArgumentException("Target position is outside the available workspace:" +
                    $"{Environment.NewLine}{targetPosition}");
            }

            if (!Limits.CheckVelocity(targetVelocity)) {
                throw new ArgumentException("target velocity exceeding max value " +
                    $"({Limits.VelocityLimit.XYZ} [mm/s], {Limits.VelocityLimit.ABC} [deg/s]):" +
                    $"{Environment.NewLine}{targetVelocity}");
            }

            lock (forceMoveSyncLock) {
                if (isForceMoveModeEnabled) {
                    return;
                }
            }

            generator.SetTargetPosition(position, targetPosition, targetVelocity, targetDuration);
        }

        /// <summary>
        /// Moves robot to the specified position and blocks current thread until position is reached.
        /// Enables force move mode during the movement.
        /// </summary>
        /// <param name="targetPosition">target position</param>
        /// <param name="targetVelocity">target velocity (velocity after targetDuration)</param>
        /// <param name="targetDuration">desired movement duration in seconds</param>
        public void ForceMoveTo(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            MoveTo(targetPosition, targetVelocity, targetDuration);

            lock (forceMoveSyncLock) {
                isForceMoveModeEnabled = true;
            }

            ManualResetEvent targetPositionReached = new ManualResetEvent(false);

            void processFrame(object sender, FrameReceivedEventArgs args) {
                if (args.IsTargetPositionReached) {
                    targetPositionReached.Set();
                }
            };

            FrameReceived += processFrame;
            targetPositionReached.WaitOne();
            FrameReceived -= processFrame;

            lock (forceMoveSyncLock) {
                isForceMoveModeEnabled = false;
            }
        }

        /// <summary>
        /// Shifts robot by the specified delta position
        /// </summary>
        /// <param name="deltaPosition">desired position change</param>
        /// <param name="targetVelocity">target velocity (velocity after targetDuration)</param>
        /// <param name="targetDuration">desired movement duration in seconds</param>
        public void Shift(RobotVector deltaPosition, RobotVector targetVelocity, double targetDuration) {
            MoveTo(Position + deltaPosition, targetVelocity, targetDuration);
        }

        /// <summary>
        /// Shifts robot by the specified delta position and blocks current thread until new position is reached.
        /// Enables force move mode during the movement.
        /// </summary>
        /// <param name="deltaPosition">desired position change</param>
        /// <param name="targetVelocity">target velocity (velocity after targetDuration)</param>
        /// <param name="targetDuration">desired movement duration in seconds</param>
        public void ForceShift(RobotVector deltaPosition, RobotVector targetVelocity, double targetDuration) {
            ForceMoveTo(Position + deltaPosition, targetVelocity, targetDuration);
        }

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
                return $"0.0.0.0:000";
            }
        }

    }
}