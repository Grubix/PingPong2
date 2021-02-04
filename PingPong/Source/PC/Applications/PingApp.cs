using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.Applications {
    public class PingApp : IApplication<PingDataReadyEventArgs> {

        private readonly object settingsSyncLock = new object();

        private readonly Robot robot;

        private readonly OptiTrackSystem optiTrack;

        private readonly HitPrediction prediction;

        private readonly Func<Vector<double>, bool> checkFunction;

        private readonly List<Vector<double>> samplesBuffer = new List<Vector<double>>();

        private readonly PIRegulator regB;

        private readonly PIRegulator regC;

        private readonly PIRegulator regZ;

        private CancellationTokenSource cts;

        private bool isStarted;

        private bool waitingForBallToHit;

        private int bouncesCounter;

        private double elapsedTime;

        private double currentBounceHeight;

        public (double Kp, double Ki) XAxisRegulatorParams {
            set {
                lock (settingsSyncLock) {
                    regB.SetParams(value.Kp, value.Ki, regB.SetPoint);
                }
            }
        }

        public (double Kp, double Ki) YAxisRegulatorParams {
            set {
                lock (settingsSyncLock) {
                    regC.SetParams(value.Kp, value.Ki, regC.SetPoint);
                }
            }
        }

        public double TargetBounceHeigth {
            set {
                lock (settingsSyncLock) {
                    regZ.SetParams(regZ.Kp, regZ.Ki, value);
                }
            }
        }

        #region events

        public event EventHandler Started;

        public event EventHandler Stopped;

        public event EventHandler<PingDataReadyEventArgs> DataReady;

        #endregion

        public PingApp(Robot robot, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot = robot;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;

            regB = new PIRegulator(0.008, 0.04, 0.44);
            regC = new PIRegulator(0.008, 0.04, 900);
            regZ = new PIRegulator(0.008, 0.04, 1000);

            currentBounceHeight = 1000;
            prediction = new HitPrediction();
            prediction.Reset(180);
        }

        ~PingApp() {
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

            if (robot.OptiTrackTransformation == null) {
                throw new InvalidOperationException("OptiTrack transformation must not be null");
            }

            if (!robot.IsTargetPositionReached) {
                throw new InvalidOperationException("Cos tam ze robot musi stac w miejscu w momencie startu");
            }

            if (cts != null) {
                cts.Cancel();
            }

            cts = new CancellationTokenSource();
            Task.Run(() => {
                try {
                    Task.Run(() => {
                        ManualResetEvent ballSpottedEvent = new ManualResetEvent(false);
                        Vector<double> translation = robot.OptiTrackTransformation.Translation;

                        void checkBallVisiblity(object sender, OptiTrack.FrameReceivedEventArgs args) {
                            var robotBaseBallPosition = robot.OptiTrackTransformation.Convert(args.BallPosition);

                            bool isBallVisible =
                                args.BallPosition[0] != 0 &&
                                args.BallPosition[1] != 0 &&
                                args.BallPosition[2] != 0;

                            bool ballPositionChanged =
                                args.BallPosition[0] != args.PrevBallPosition[0] ||
                                args.BallPosition[1] != args.PrevBallPosition[1] ||
                                args.BallPosition[2] != args.PrevBallPosition[2];

                            if (isBallVisible && ballPositionChanged && checkFunction.Invoke(robotBaseBallPosition)) {
                                optiTrack.FrameReceived -= checkBallVisiblity;
                                ballSpottedEvent.Set();
                            }
                        }

                        // wait for ballSpottedEvent.Set() signal
                        optiTrack.FrameReceived += checkBallVisiblity;
                        ballSpottedEvent.WaitOne();

                        // start application
                        isStarted = true;
                        optiTrack.FrameReceived += ProcessOptiTrackFrame;
                        Started?.Invoke(this, EventArgs.Empty);
                    }).Wait(cts.Token);
                } catch (Exception) {
                    return;
                }
            });
        }

        public void Stop() {
            if (isStarted) {
                isStarted = false;

                Task.Run(() => {
                    robot.ForceMoveTo(robot.HomePosition, RobotVector.Zero, 3);
                    Stopped?.Invoke(this, EventArgs.Empty);
                });
            } else {
                if (cts != null) {
                    cts.Cancel();
                }
            }

            optiTrack.FrameReceived -= ProcessOptiTrackFrame;
        }

        private void ProcessOptiTrackFrame(object sender, OptiTrack.FrameReceivedEventArgs args) {
            // Convert ball position to robot coordinate system
            var ballPosition = robot.OptiTrackTransformation.Convert(args.BallPosition);
            var prevBallPosition = robot.OptiTrackTransformation.Convert(args.PrevBallPosition);

            // Stop application if ball fell
            if (ballPosition[2] < prediction.TargetHitHeight - 50.0) {
                Stop();
                return;
            }

            PingDataReadyEventArgs data = new PingDataReadyEventArgs {
                PredictedBallPosition = prediction.Position,
                PredictedBallVelocity = prediction.Velocity,
                ActualBallPosition = ballPosition,
                ActualRobotPosition = robot.Position,
                PredictedTimeToHit = prediction.TimeToHit,
                BounceCounter = bouncesCounter,
                LastBounceHeight = currentBounceHeight
            };

            bool positionChanged =
                ballPosition[0] != prevBallPosition[0] ||
                ballPosition[1] != prevBallPosition[1] ||
                ballPosition[2] != prevBallPosition[2];

            if (!positionChanged) {
                if (prediction.SamplesCount != 0) {
                    elapsedTime += args.FrameDeltaTime;
                }

                DataReady?.Invoke(this, data);
                return;
            }

            if (waitingForBallToHit) {
                prediction.Reset(180);
                elapsedTime = 0;

                if (samplesBuffer.Count == 5) {
                    samplesBuffer.Add(ballPosition);
                    samplesBuffer.RemoveAt(0);
                } else {
                    samplesBuffer.Add(ballPosition);
                    DataReady?.Invoke(this, data);
                    return;
                }

                for (int i = 1; i < samplesBuffer.Count; i++) {
                    if (samplesBuffer[i][2] <= samplesBuffer[i - 1][2]) {
                        DataReady?.Invoke(this, data);
                        return;
                    }
                }

                samplesBuffer.Clear();
                waitingForBallToHit = false;
            }

            if (prediction.SamplesCount != 0) {
                elapsedTime += args.FrameDeltaTime;
            }

            prediction.AddMeasurement(ballPosition, elapsedTime);
            data.PredictedBallPosition = prediction.Position;
            data.PredictedBallVelocity = prediction.Velocity;
            data.PredictedTimeToHit = prediction.TimeToHit;

            samplesBuffer.Add(ballPosition);

            if (samplesBuffer.Count == 5) {
                samplesBuffer.RemoveAt(0);

                bool bounceHeightFound = true;

                for (int i = 1; i < samplesBuffer.Count; i++) {
                    if (samplesBuffer[i][2] >= samplesBuffer[i - 1][2]) {
                        bounceHeightFound = false;
                        break;
                    }
                }

                if (bounceHeightFound) {
                    currentBounceHeight = samplesBuffer[0][2];
                    data.LastBounceHeight = currentBounceHeight;
                }
            }

            if (prediction.IsReady) {
                if (prediction.TimeToHit >= 0.25) {
                    var upVector = Vector<double>.Build.DenseOfArray(new double[] { 0, 0, 1 });
                    var paddleNormalVector = upVector - Normalize(prediction.Velocity);

                    double angleB = Math.Atan2(paddleNormalVector[0], paddleNormalVector[2]) * 180.0 / Math.PI - 0.89;
                    double angleC = -90.0 - Math.Atan2(paddleNormalVector[1], paddleNormalVector[2]) * 180.0 / Math.PI - 0.5;
                    double speedZ = 450.0;
                    //speed = (Math.Sqrt(2.0 * 9.81 * (regZ.SetPoint - 180.0) * 1000) + prediction.Velocity[2] * 0.8) / (1 + 0.8);

                    lock (settingsSyncLock) {
                        angleB += regB.ComputeU(prediction.Position[0]);
                        angleC -= regC.ComputeU(prediction.Position[1]);
                        //speed += regZ.ComputeU(currentBounceHeight);
                    }

                    var fallDir = Vector<double>.Build.DenseOfArray(new double[] {
                        prediction.Velocity[0] - 0,
                        prediction.Velocity[1] - 0,
                        prediction.Velocity[2] - speedZ
                    });

                    angleB = Math.Min(Math.Max(angleB, -15.0), 15.0);
                    angleC = Math.Min(Math.Max(angleC, -100.0), -80.0);
                    speedZ = Math.Min(Math.Max(speedZ, 450.0), 600.0); // DOBRZE TEN ZAKRES ?

                    //if (angleB == -15.0 || angleB == 15.0 || angleC == -100.0 || angleC == -80.0) {
                    //    Console.WriteLine("NASYCENIE na kącie!!!!!!!!!");
                    //}

                    double dampCoeff = 1;

                    if (prediction.TimeToHit >= 0.4) {
                        dampCoeff = Math.Exp(-(prediction.TimeToHit - 0.4) / 0.15);
                    }

                    RobotVector robotActualPosition = robot.Position;
                    RobotVector robotTargetPosition = new RobotVector(
                        robotActualPosition.X + (prediction.Position[0] - robotActualPosition.X) * dampCoeff,
                        robotActualPosition.Y + (prediction.Position[1] - robotActualPosition.Y) * dampCoeff,
                        prediction.Position[2],
                        0,
                        robotActualPosition.B + (angleB - robotActualPosition.B) * dampCoeff,
                        robotActualPosition.C + (angleC - robotActualPosition.C) * dampCoeff
                    );

                    RobotVector robotTargetVelocity = new RobotVector(0, 0, speedZ);

                    if (robot.Limits.CheckMovement(robotTargetPosition, robotTargetVelocity, prediction.TimeToHit)) {
                        RobotMovement movement1 = new RobotMovement(robotTargetPosition, robotTargetVelocity, prediction.TimeToHit);
                        RobotMovement movement2;

                        if (bouncesCounter > 3) {
                            movement2 = new RobotMovement(
                                new RobotVector(robotActualPosition.X, robotActualPosition.Y, prediction.TargetHitHeight - 10, robot.HomePosition.ABC),
                                RobotVector.Zero,
                                1.0
                            );
                        } else {
                            movement2 = new RobotMovement(robot.HomePosition, RobotVector.Zero, 1.0);
                        }

                        robot.MoveTo(new RobotMovement[] { movement1, movement2 });
                    }
                } else {
                    regB.Shift();
                    regC.Shift();
                    regZ.Shift();
                    waitingForBallToHit = true;
                    prediction.Reset(180);
                    elapsedTime = 0;
                    bouncesCounter++;

                    data.BounceCounter = bouncesCounter;
                }
            }

            DataReady?.Invoke(this, data);
        }

        private Vector<double> Normalize(Vector<double> vec) {
            double norm = 0.0;
            for (int i = 0; i < vec.Count; i++) {
                norm += vec[i] * vec[i];
            }
            norm = Math.Sqrt(norm);

            return vec / norm;
        }

    }
}