using PingPong.Maths;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.KUKA {

    public class KUKARobot : IDevice {

        private readonly object receivedDataSyncLock = new object();

        private readonly object forceMoveSyncLock = new object();

        private readonly BackgroundWorker worker;

        private readonly RSIAdapter rsiAdapter;

        private readonly TrajectoryGenerator5 generator;

        private CancellationTokenSource cancellationTokenSource;

        private bool isInitialized = false;

        private bool forceMoveMode = false;

        private long IPOC; // timestamp

        private RobotVector position;

        private RobotAxisPosition axisPosition;

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
                return rsiAdapter.Ip;
            }
        }

        //TODO: port powinien byc brany z rsi adaptera
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
        /// Robot actual position error
        /// </summary>
        public RobotVector PositionError {
            get {
                return generator.PositionError;
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

        public KUKARobot(RobotConfig config) {
            rsiAdapter = new RSIAdapter();
            generator = new TrajectoryGenerator5();
            Config = config;

            position = RobotVector.Zero;
            axisPosition = RobotAxisPosition.Zero;

            worker = new BackgroundWorker() {
                WorkerSupportsCancellation = true
            };

            worker.DoWork += async (sender, args) => {
                cancellationTokenSource = new CancellationTokenSource();
                InputFrame receivedFrame = null;

                try {
                    // Connect with the robot
                    Task connectTask = Task.Run(async () => {
                        receivedFrame = await rsiAdapter.Connect(Config.Port);
                    });

                    connectTask.Wait(cancellationTokenSource.Token);
                } catch (OperationCanceledException) {
                    // Connect operation cancelled (Disconnect() method)
                    rsiAdapter.Disconnect();
                    return;
                }

                generator.Restart(receivedFrame.Position);

                lock (receivedDataSyncLock) {
                    IPOC = receivedFrame.IPOC;
                    position = receivedFrame.Position;
                    HomePosition = receivedFrame.Position;
                }

                // Send first response
                rsiAdapter.SendData(new OutputFrame() {
                    Correction = new RobotVector(),
                    IPOC = IPOC
                });

                isInitialized = true;
                Initialized?.Invoke();

                // Start loop for receiving and sending data
                while (!worker.CancellationPending) {
                    await ReceiveDataAsync();
                    SendData();
                }

                isInitialized = false;
                rsiAdapter.Disconnect();

                Uninitialized?.Invoke();
            };
        }

        public KUKARobot() : this(null) {
        }

        /// <summary>
        /// Receives data (IPOC, cartesian and axis position) from the robot asynchronously, 
        /// raises <see cref="KUKARobot.FrameRecived">FrameReceived</see> event
        /// </summary>
        private async Task ReceiveDataAsync() {
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

            //TODO: mozna dorobic sprawdzanie korekcji (dla IsTargetPositonReached = true) zeby nie wywalal jak robot stoi

            lock (receivedDataSyncLock) {
                IPOC = receivedFrame.IPOC;
                position = receivedFrame.Position;
                axisPosition = receivedFrame.AxisPosition;
            }

            FrameReceived?.Invoke(receivedFrame);
        }

        /// <summary>
        /// Sends data (IPOC, correction) to the robot, raises <see cref="KUKARobot.FrameSent">FrameSent</see> event
        /// </summary>
        private void SendData() {
            RobotVector correction = generator.GetNextCorrection(position); ;

            // ZMIANA NA CHECK ABSOLUTE CORRECTION DLA GENERATORA ABS.
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

            generator.SetTargetPosition(targetPosition, targetVelocity, targetDuration);
        }

        // TODO: async, Task, token source ?
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

        // TODO: async, Task, token source ?
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
            if (cancellationTokenSource != null) {
                cancellationTokenSource.Cancel();
            }

            worker.CancelAsync();
        }

        public bool IsInitialized() {
            return isInitialized;
        }

        public override string ToString() {
            if (Ip != null) {
                return $"{Ip}:{Port}";
            } else {
                return $"0.0.0.0:{Port}";
            }
        }

    }
}