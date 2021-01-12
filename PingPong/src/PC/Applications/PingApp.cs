using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.OptiTrack;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.Applications {
    class PingApp : IApplication {

        private bool isStarted;

        private readonly KUKARobot robot;

        private readonly OptiTrackSystem optiTrack;

        private readonly HitPrediction prediction;

        private readonly Func<Vector<double>, bool> checkFunction;

        private readonly Vector<double> upVector;

        private readonly object syncLock = new object();

        private bool robotMovedToHitPosition;

        private double elapsedTime = 0;

        public PingApp(KUKARobot robot, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot = robot;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;

            prediction = new HitPrediction();
            upVector = Vector<double>.Build.DenseOfArray(
                new double[] { 0.0, 0.0, 1.0 }
            );
        }

        ~PingApp() {
            Stop();
        }

        public void Start() {
            if (isStarted) {
                return;
            }

            if (!robot.IsInitialized() || !optiTrack.IsInitialized()) {
                throw new InvalidOperationException("Robot and optiTrack system must be initialized");
            }

            // waiting for ball to be visible
            Task.Run(() => {
                ManualResetEvent ballSpottedEvent = new ManualResetEvent(false);
                Vector<double> ballPosition = null;
                Vector<double> prevBallPosition = null;
                bool firstFrame = true;

                void checkBallVisiblity(OptiTrack.InputFrame frame) {
                    if (firstFrame) {
                        firstFrame = false;
                        prevBallPosition = frame.BallPosition;
                        return;
                    }

                    ballPosition = robot.OptiTrackTransformation.Convert(frame.BallPosition);

                    bool positionChanged =
                        ballPosition[0] != prevBallPosition[0] ||
                        ballPosition[1] != prevBallPosition[1] ||
                        ballPosition[2] != prevBallPosition[2];

                    if (positionChanged && checkFunction.Invoke(ballPosition)) {
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
                isStarted = true;
                robot.FrameReceived += ProcessRobotFrame;
                optiTrack.FrameReceived += ProcessOptiTrackFrame;
            });
        }

        public void Stop() {
            isStarted = false;

            robot.FrameReceived -= ProcessRobotFrame;
            optiTrack.FrameReceived -= ProcessOptiTrackFrame;
            robot.Uninitialize();
        }

        private void ProcessRobotFrame(KUKA.InputFrame frame) {
            lock (syncLock) {
                if (robotMovedToHitPosition && robot.IsTargetPositionReached) {
                    // Slow down the robot
                    robotMovedToHitPosition = false;
                    robot.MoveTo(new RobotVector(robot.Position.XYZ, robot.HomePosition.ABC), RobotVector.Zero, 3);
                }
            }
        }

        private void ProcessOptiTrackFrame(OptiTrack.InputFrame frame) {
            var robotBaseBallPosition = robot.OptiTrackTransformation.Convert(frame.BallPosition);

            // if true -> ball fell, stop application
            if (robotBaseBallPosition[2] < prediction.TargetHitHeight - 50.0) {
                Stop();
                return;
            }

            if (prediction.PolyfitSamplesCount != 0) {
                elapsedTime += frame.DeltaTime;
            }

            prediction.AddMeasurement(robotBaseBallPosition, elapsedTime);

            if (prediction.IsReady && prediction.TimeToHit >= 0.3) {
                prediction.Calculate();

                var predBallPosition = prediction.Position; // predicted ball position on hit
                var predBallVelocity = prediction.Velocity; // predicted ball velocity on hit
                var racketNormalVector = upVector.Normalize(1.0) - predBallVelocity.Normalize(1.0);

                double angleB = Math.Atan2(racketNormalVector[0], racketNormalVector[2]) * 180.0 / Math.PI;
                double angleC = -90.0 - Math.Atan2(racketNormalVector[1], racketNormalVector[2]) * 180.0 / Math.PI;

                //TODO: wykorzystanie pida czy czegokolwiek innego zeby skorygowac katy abc (odbicie do zadanego targeta)

                RobotVector robotTargetPostion = new RobotVector(
                    predBallPosition[0], predBallPosition[1], predBallPosition[2], 0, angleB, angleC
                );

                if (robot.Limits.CheckPosition(robotTargetPostion)) {
                    lock (syncLock) {
                        robotMovedToHitPosition = true;
                        robot.MoveTo(robotTargetPostion, RobotVector.Zero, prediction.TimeToHit);
                    }
                }
            }
        }

        public bool IsStarted() {
            return isStarted;
        }

    }
}