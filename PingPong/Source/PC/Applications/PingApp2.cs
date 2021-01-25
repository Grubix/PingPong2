using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.OptiTrack;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.Applications {
    class PingApp2 : IApplication<PingDataReadyEventArgs> {

        private readonly Robot robot;

        private readonly OptiTrackSystem optiTrack;

        private readonly HitPrediction prediction;

        private readonly Func<Vector<double>, bool> checkFunction;

        private readonly object syncLock = new object();

        private bool isStarted;

        private double elapsedTime;

        private Vector<double> prevBallPosition;

        private Vector<double> currentBallPosition;

        private bool robotMovedToHitPosition;

        public event EventHandler Started;

        public event EventHandler Stopped;

        public event EventHandler<PingDataReadyEventArgs> DataReady;

        public PingApp2(Robot robot, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot = robot;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;

            prediction = new HitPrediction();
            prediction.Reset(180);
        }

        ~PingApp2() {
            Stop();
        }

        public bool IsStarted() {
            return isStarted;
        }

        public void Start() {
            if (isStarted) {
                return;
            }

            if (!robot.IsInitialized() || !optiTrack.IsInitialized()) {
                throw new InvalidOperationException("Robot and optiTrack system must be initialized");
            }

            isStarted = true;

            // waiting for ball to be visible
            Task.Run(() => {
                ManualResetEvent ballSpottedEvent = new ManualResetEvent(false);
                Vector<double> ballPosition = null;
                Vector<double> prevBallPosition = null;
                Vector<double> translation = robot.OptiTrackTransformation.Translation;

                bool firstFrame = true;

                void checkBallVisiblity(object sender, OptiTrack.FrameReceivedEventArgs args) {
                    if (firstFrame) {
                        firstFrame = false;
                        prevBallPosition = args.BallPosition;
                        return;
                    }

                    ballPosition = robot.OptiTrackTransformation.Convert(args.BallPosition);
                    prevBallPosition = ballPosition;

                    bool ballVisible =
                        ballPosition[0] != translation[0] &&
                        ballPosition[1] != translation[1] &&
                        ballPosition[2] != translation[2];

                    bool ballPositionChanged =
                        ballPosition[0] != prevBallPosition[0] ||
                        ballPosition[1] != prevBallPosition[1] ||
                        ballPosition[2] != prevBallPosition[2];

                    if (ballVisible && ballPositionChanged && checkFunction.Invoke(ballPosition)) {
                        optiTrack.FrameReceived -= checkBallVisiblity;
                        ballSpottedEvent.Set();
                    } else {
                        prevBallPosition = ballPosition;
                    }
                }

                // wait for ballSpottedEvent.Set() signal
                optiTrack.FrameReceived += checkBallVisiblity;
                ballSpottedEvent.WaitOne();

                // start application
                robot.FrameReceived += ProcessRobotFrame;
                optiTrack.FrameReceived += ProcessOptiTrackFrame;
                Started?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Stop() {
            if (isStarted) {
                isStarted = false;

                robot.FrameReceived -= ProcessRobotFrame;
                optiTrack.FrameReceived -= ProcessOptiTrackFrame;
                robot.Uninitialize();

                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        // Robot thread
        private void ProcessRobotFrame(object sender, KUKA.FrameReceivedEventArgs args) {
            Vector<double> ballPosition;
            Vector<double> predBallPositionOnHit;
            Vector<double> predBallVelocityOnHit;
            double predTimeToHit;
            bool isPredictionReady;

            lock (syncLock) {
                ballPosition = currentBallPosition;
                predBallPositionOnHit = prediction.Position;
                predBallVelocityOnHit = prediction.Position;
                predTimeToHit = prediction.TimeToHit;
                isPredictionReady = prediction.IsReady;
            }

            if (robotMovedToHitPosition) {
                if (robot.IsTargetPositionReached) { //czekanie az robot dojedzie do zadanego punktu, albo trzeba jakos wykryc odbicie z optitracka
                    robotMovedToHitPosition = false;
                    robot.MoveTo(robot.HomePosition, RobotVector.Zero, 3); //hamowanie

                    lock (syncLock) { // reset predykcji
                        elapsedTime = 0;
                        prediction.Reset(180);
                    }
                }
            } else if (isPredictionReady && predTimeToHit >= 0.25) {
                RobotVector targetPosition = new RobotVector(
                    predBallPositionOnHit[0],
                    predBallPositionOnHit[1],
                    predBallPositionOnHit[2],
                    0,
                    0, //TODO: B
                    -90 //TODO: C
                );

                RobotVector targetVelocity = new RobotVector(
                    0, //TODO: vX
                    0, //TODO: vY
                    0 //TODO: vZ
                );

                if (robot.Limits.CheckMove(targetPosition, targetVelocity, predTimeToHit)) {
                    robot.MoveTo(targetPosition, targetVelocity, predTimeToHit);
                    robotMovedToHitPosition = true;
                }
            }

            DataReady?.Invoke(this, new PingDataReadyEventArgs {
                PredictedBallPosition = predBallPositionOnHit,
                PredictedBallVelocity = predBallVelocityOnHit,
                ActualBallPosition = currentBallPosition,
                ActualRobotPosition = args.Position,
                PredictedTimeToHit = predTimeToHit,
                BallSetpointX = 0, //TODO:
                BallSetpointY = 0, //TODO:
                BallSetpointZ = 0 //TODO:
            });
        }

        // OptiTrack thread
        private void ProcessOptiTrackFrame(object sender, OptiTrack.FrameReceivedEventArgs args) {
            lock (syncLock) {
                currentBallPosition = robot.OptiTrackTransformation.Convert(args.BallPosition);

                if (currentBallPosition[2] < prediction.TargetHitHeight - 50.0) {
                    Stop();
                    return;
                }

                bool positionChanged =
                    currentBallPosition[0] != prevBallPosition[0] ||
                    currentBallPosition[1] != prevBallPosition[1] ||
                    currentBallPosition[2] != prevBallPosition[2];

                prevBallPosition = currentBallPosition;

                if (!positionChanged) {
                    if (prediction.SamplesCount != 0) {
                        elapsedTime += args.ReceivedFrame.DeltaTime;
                    }

                    return;
                }

                if (prediction.SamplesCount != 0) {
                    elapsedTime += args.ReceivedFrame.DeltaTime;
                }

                prediction.AddMeasurement(currentBallPosition, elapsedTime);
            }
        }

    }
}