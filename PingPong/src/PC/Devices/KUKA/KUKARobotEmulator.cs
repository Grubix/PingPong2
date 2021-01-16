using PingPong.Maths;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.KUKA {
    class KUKARobotEmulator : IDevice {

        private readonly object receivedDataSyncLock = new object();

        private readonly object forceMoveSyncLock = new object();

        private readonly BackgroundWorker worker;

        private readonly TrajectoryGenerator5T generator;

        private readonly Random rand = new Random();

        private bool isInitialized = false;

        private bool forceMoveMode = false;

        private long IPOC; // timestamp

        private RobotVector position;

        private RobotVector correction;

        private RobotConfig config;

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
                return "x.x.x.x";
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
                    return RobotAxisPosition.Zero;
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
        public event Action Initialized;

        /// <summary>
        /// TODO
        /// </summary>
        public event Action Uninitialized;

        /// <summary>
        /// Occurs when <see cref="InputFrame"/> frame is received
        /// </summary>
        public event Action<InputFrame> FrameReceived;

        /// <summary>
        /// Occurs when <see cref="OutputFrame"/> frame is sent
        /// </summary>
        public event Action<OutputFrame> FrameSent;

        public KUKARobotEmulator(RobotConfig config) {
            generator = new TrajectoryGenerator5T();
            Config = config;

            position = RobotVector.Zero;
            correction = RobotVector.Zero;

            worker = new BackgroundWorker() {
                WorkerSupportsCancellation = true
            };

            worker.DoWork += (sender, args) => {
                InputFrame receivedFrame = new InputFrame {
                    IPOC = 0,
                    Position = RobotVector.Zero,
                    AxisPosition = RobotAxisPosition.Zero
                };

                Console.WriteLine(receivedFrame.Position == null);
                generator.Initialize(receivedFrame.Position);

                lock (receivedDataSyncLock) {
                    IPOC = receivedFrame.IPOC;
                    position = receivedFrame.Position;
                    HomePosition = receivedFrame.Position;
                }

                // Send first response
                OutputFrame response = new OutputFrame() {
                    Correction = new RobotVector(),
                    IPOC = IPOC
                };

                isInitialized = true;
                Initialized?.Invoke();

                // Start loop for receiving and sending data
                while (!worker.CancellationPending) {
                    ReceiveDataAsync();
                    SendData();
                    Thread.Sleep(4);
                }

                isInitialized = false;
                Uninitialized?.Invoke();
            };
        }

        public KUKARobotEmulator() : this(null) {
        }

        /// <summary>
        /// Receives data (IPOC, cartesian and axis position) from the robot asynchronously, 
        /// raises <see cref="KUKARobot.FrameRecived">FrameReceived</see> event
        /// </summary>
        private void ReceiveDataAsync() {
            RobotVector noise = new RobotVector(
                rand.NextDouble() * 0.1 - 0.05,
                0,
                0,
                0,
                0,
                0
            );

            InputFrame receivedFrame = new InputFrame {
                IPOC = IPOC + 4,
                Position = position + correction + noise,
                AxisPosition = RobotAxisPosition.Zero
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
                IPOC = receivedFrame.IPOC;
                position = receivedFrame.Position;
            }

            FrameReceived?.Invoke(receivedFrame);
        }

        /// <summary>
        /// Sends data (IPOC, correction) to the robot, raises <see cref="KUKARobot.FrameSent">FrameSent</see> event
        /// </summary>
        private void SendData() {
            correction = generator.GetNextCorrection();

            if (!Limits.CheckRelativeCorrection(correction)) {
                Uninitialize();
                throw new InvalidOperationException("Correction limit has been exceeded:" +
                    $"{Environment.NewLine}{correction}");
            }

            OutputFrame outputFrame = new OutputFrame() {
                Correction = correction,
                IPOC = IPOC
            };

            FrameSent?.Invoke(outputFrame);
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
                if (forceMoveMode) {
                    return;
                }
            }

            generator.SetTargetPosition(Position, targetPosition, targetVelocity, targetDuration);
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
                forceMoveMode = true;
            }

            ManualResetEvent targetPositionReached = new ManualResetEvent(false);

            void processFrame(InputFrame frameReceived) {
                if (generator.IsTargetPositionReached) {
                    targetPositionReached.Set();
                }
            };

            FrameReceived += processFrame;
            targetPositionReached.WaitOne();
            FrameReceived -= processFrame;

            lock (forceMoveSyncLock) {
                forceMoveMode = false;
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
            if (!isInitialized) {
                if (config == null) {
                    throw new InvalidOperationException("Robot configuration is not set.");
                }

                worker.RunWorkerAsync();
            }
        }

        public void Uninitialize() {
            worker.CancelAsync();
        }

        public bool IsInitialized() {
            return isInitialized;
        }

        public override string ToString() {
            return "x.x.x.x:yyyy";
        }

    }
}
