using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.OptiTrack;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.Applications {
    class PingApp : IApplication<PingAppData> {

        private bool isStarted;

        private readonly Robot robot;

        private readonly OptiTrackSystem optiTrack;

        private readonly HitPrediction prediction;

        private readonly Func<Vector<double>, bool> checkFunction;

        private Vector<double> upVector;

        private readonly object syncLock = new object();

        private bool robotMovedToHitPosition;

        private double elapsedTime = 0;

        private double maxBallHeigth = 1500;

        private Vector<double> destBallPosition;

        private readonly double CoR = 0.8;

        public event Action Started;

        public event Action Stopped;

        public event Action<PingAppData> DataReady;

        public PingApp(Robot robot, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot = robot;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;

            prediction = new HitPrediction();
            prediction.Reset(183.48);

            upVector = Vector<double>.Build.DenseOfArray(
                new double[] { 0.0, 0.0, 1.0 }
            );
            destBallPosition = Vector<double>.Build.DenseOfArray(
                new double[] { 0.44, 793.19, 177.82 }
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

            isStarted = true;

            // waiting for ball to be visible
            Task.Run(() => {
                ManualResetEvent ballSpottedEvent = new ManualResetEvent(false);
                Vector<double> ballPosition = null;
                Vector<double> prevBallPosition = null;
                Vector<double> translation = robot.OptiTrackTransformation.Translation;

                bool firstFrame = true;

                void checkBallVisiblity(OptiTrack.InputFrame frame) {
                    if (firstFrame) {
                        firstFrame = false;
                        prevBallPosition = frame.BallPosition;
                        return;
                    }

                    ballPosition = robot.OptiTrackTransformation.Convert(frame.BallPosition);

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
                Started?.Invoke();
            });
        }

        //TODO: MEGA WAZNE ANULACJA TASKA UTWORZONEGO W START()
        public void Stop() {
            if (isStarted) {
                isStarted = false;

                robot.FrameReceived -= ProcessRobotFrame;
                optiTrack.FrameReceived -= ProcessOptiTrackFrame;
                //robot.Uninitialize();

                Stopped?.Invoke();
            }
        }

        private void ProcessRobotFrame(KUKA.InputFrame frame) {
            lock (syncLock) {
                if (robotMovedToHitPosition && robot.IsTargetPositionReached) {
                    // Slow down the robot
                    robotMovedToHitPosition = false;
                    robot.MoveTo(robot.HomePosition, RobotVector.Zero, 3);
                    //Stop();

                    robot.FrameReceived -= ProcessRobotFrame;
                    prediction.Reset(prediction.TargetHitHeight);
                    elapsedTime = 0;
                    //jakas_flaga = true;
                }
            }
        }

        private void ProcessOptiTrackFrame(OptiTrack.InputFrame frame) {
            Vector<double> robotBaseBallPosition = robot.OptiTrackTransformation.Convert(frame.BallPosition);

            // if true -> ball fell, stop application
            if (robotBaseBallPosition[2] < prediction.TargetHitHeight - 50.0) {
                Stop();
                return;
            }

            RobotVector robotActualPosition = robot.Position;
            Vector<double> predBallPosition;
            Vector<double> predBallVelocity;
            double predTimeToHit;
            bool isPredictionReady;

            lock (syncLock) {
                if (prediction.SamplesCount != 0) {
                    elapsedTime += frame.DeltaTime;
                }

                prediction.AddMeasurement(robotBaseBallPosition, elapsedTime);

                predBallPosition = prediction.Position;
                predBallVelocity = prediction.Velocity;
                predTimeToHit = prediction.TimeToHit;
                isPredictionReady = prediction.IsReady;
            }

            if (isPredictionReady && predTimeToHit >= 0.3) {
                double t1 = Math.Sqrt(2.0 / 9.81 * (maxBallHeigth - predBallPosition[2]) / 1000.0);
                double t2 = Math.Sqrt(2.0 / 9.81 * (maxBallHeigth - destBallPosition[2]) / 1000.0);
                double t = t1 + t2;

                upVector = Vector<double>.Build.DenseOfArray(new double[] {
                    (destBallPosition[0] - predBallPosition[0]) / t,
                    (destBallPosition[1] - predBallPosition[1]) / t,
                    Math.Sqrt(2.0 * 9.81 * 1000 * (maxBallHeigth - predBallPosition[2]))
                });

                var racketNormalVector = upVector.Normalize(1.0) - predBallVelocity.Normalize(1.0);

                double angleB = Math.Atan2(racketNormalVector[0], racketNormalVector[2]) * 180.0 / Math.PI;
                double angleC = -90.0 - Math.Atan2(racketNormalVector[1], racketNormalVector[2]) * 180.0 / Math.PI;

                // NIE LEGITNE
                var robotTargetVelocity = new RobotVector(
                    (upVector[0] + CoR * predBallVelocity[0]) / (1 + CoR),
                    (upVector[1] + CoR * predBallVelocity[1]) / (1 + CoR),
                    (upVector[2] + CoR * predBallVelocity[2]) / (1 + CoR)
                );

                double dampCoeff = 1;

                if (predTimeToHit >= 0.4) {
                    dampCoeff = Math.Exp(-(predTimeToHit - 0.4) / 0.15);
                }

                RobotVector robotTargetPostion = new RobotVector(
                    robotActualPosition.X + (predBallPosition[0] - robotActualPosition.X) * dampCoeff,
                    robotActualPosition.Y + (predBallPosition[1] - robotActualPosition.Y) * dampCoeff,
                    predBallPosition[2],
                    0,
                    robotActualPosition.B + (angleB - robotActualPosition.B) * dampCoeff,
                    robotActualPosition.C + (angleC - robotActualPosition.C) * dampCoeff
                );

                if (robot.Limits.CheckPosition(robotTargetPostion) && Math.Abs(angleB) < 30.0 && angleC < -75 && angleC > -120) {
                    lock (syncLock) {
                        robotMovedToHitPosition = true;
                        robot.MoveTo(robotTargetPostion, new RobotVector(0, 0, 0), predTimeToHit);
                    }
                }
            }

            PingAppData data = new PingAppData {
                PredictedBallPosition = predBallPosition,
                PredictedBallVelocity = predBallVelocity,
                ActualBallPosition =  robotBaseBallPosition,
                ActualRobotPosition = robotActualPosition,
                PredictedTimeToHit = predTimeToHit,
                BallSetpointX = destBallPosition[0],
                BallSetpointY = destBallPosition[1],
                BallSetpointZ = destBallPosition[2]
            };

            DataReady?.Invoke(data);
        }

        public bool IsStarted() {
            return isStarted;
        }

    }
}