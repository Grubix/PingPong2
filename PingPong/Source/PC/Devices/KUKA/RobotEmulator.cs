using PingPong.Maths;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.KUKA {

    public class RobotEmulator : IDevice {

        private readonly object receivedDataSyncLock = new object();

        private readonly object forceMoveSyncLock = new object();

        private readonly object cancellationSyncLock = new object();

        private readonly object generatorSyncLock = new object();

        private readonly TrajectoryGenerator generator;

        private readonly List<RobotVector> correctionBuffor;

        private RobotVector correction;

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
                return "0.0.0.0";
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
        public event EventHandler Initialized;

        public event EventHandler Uninitialized;

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        public event EventHandler<FrameSentEventArgs> FrameSent;

        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured;

        public event EventHandler<MovementChangedEventArgs> MovementChanged;

        public RobotEmulator(RobotVector homePosition) {
            HomePosition = homePosition;
            generator = new TrajectoryGenerator();
            position = RobotVector.Zero;
            axisPosition = RobotAxisVector.Zero;
            correctionBuffor = new List<RobotVector>();
            correction = RobotVector.Zero;
        }

        public RobotEmulator(RobotConfig config, RobotVector homePosition) : this(homePosition) {
            Config = config;
        }

        private void Connect() {
            IsCancellationRequested = false;
            InputFrame receivedFrame = new InputFrame {
                Position = HomePosition,
                AxisPosition = RobotAxisVector.Zero,
                IPOC = 0
            };

            generator.Initialize(receivedFrame.Position);

            lock (receivedDataSyncLock) {
                position = receivedFrame.Position;
                HomePosition = receivedFrame.Position;
            }

            isInitialized = true;
            Initialized?.Invoke(this, EventArgs.Empty);

            // Start loop for receiving and sending data

            try {
                while(!IsCancellationRequested) {
                    long IPOC = ReceiveDataAsync();
                    SendData(IPOC);
                    Thread.Sleep(4);
                }
            } catch (Exception e) {
                var args = new ErrorOccuredEventArgs {
                    RobotIp = ToString(),
                    Exception = e
                };

                ErrorOccured?.Invoke(this, args);
            }

            isInitialized = false;
            Uninitialized?.Invoke(this, EventArgs.Empty);
        }

        private long ReceiveDataAsync() {
            correctionBuffor.Add(correction);
            RobotVector currentCorrection = RobotVector.Zero;

            if (correctionBuffor.Count == 8) {
                currentCorrection = correctionBuffor[0];
                correctionBuffor.RemoveAt(0);
            }

            InputFrame receivedFrame = new InputFrame {
                Position = position + currentCorrection,
                AxisPosition = RobotAxisVector.Zero,
                IPOC = 0
            };

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
            correction = generator.GetNextCorrection();

            if (!Limits.CheckCorrection(correction)) {
                Uninitialize();
                throw new InvalidOperationException("Correction limit has been exceeded:" +
                    $"{Environment.NewLine}{correction}");
            }

            OutputFrame outputFrame = new OutputFrame() {
                Correction = correction,
                IPOC = IPOC
            };

            FrameSent?.Invoke(this, new FrameSentEventArgs {
                FrameSent = outputFrame,
                Position = position
            });
        }

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

        public void Initialize() {
            if (isInitialized) {
                return;
            }

            if (config == null) {
                throw new InvalidOperationException("Robot configuration is not set.");
            }

            Task.Run(() => {
                Connect();
            });
        }

        public void Uninitialize() {
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