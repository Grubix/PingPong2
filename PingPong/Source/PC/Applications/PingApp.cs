﻿using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.Applications {
    class PingApp : IApplication<PingDataReadyEventArgs> {

        private bool isStarted;

        private readonly Robot robot;

        private readonly OptiTrackSystem optiTrack;

        private readonly HitPrediction prediction;

        private readonly Func<Vector<double>, bool> checkFunction;

        private Vector<double> upVector;

        private readonly object syncLock = new object();

        private bool robotMovedToHitPosition;

        private double elapsedTime = 0;

        private double maxBallHeigth = 1100;

        private Vector<double> destBallPosition;

        private readonly double CoR = 0.8328;

        public event EventHandler Started;

        public event EventHandler Stopped;

        public event EventHandler<PingDataReadyEventArgs> DataReady;

        private bool koniec_odbicia = false;

        private PIRegulator regB;

        private PIRegulator regC;

        private Vector<double> prevBallPos;

        public PingApp(Robot robot, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot = robot;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;

            prediction = new HitPrediction();
            prediction.Reset(180);

            upVector = Vector<double>.Build.DenseOfArray(
                new double[] { 0.0, 0.0, 1.0 }
            );
            destBallPosition = Vector<double>.Build.DenseOfArray(
                new double[] { 0.44, 850.57, 180.0 }
            );
            regB = new PIRegulator(0.005, 0.001, 0.004, 0.44);
            regC = new PIRegulator(0.005, 0.001, 0.004, 850.57);
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

                void checkBallVisiblity(object sender, OptiTrack.FrameReceivedEventArgs args) {
                    if (firstFrame) {
                        firstFrame = false;
                        prevBallPosition = args.BallPosition;
                        return;
                    }

                    ballPosition = robot.OptiTrackTransformation.Convert(args.BallPosition);
                    this.prevBallPos = ballPosition;

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

        //TODO: MEGA WAZNE ANULACJA TASKA UTWORZONEGO W START()
        public void Stop() {
            if (isStarted) {
                isStarted = false;

                robot.FrameReceived -= ProcessRobotFrame;
                //optiTrack.FrameReceived -= ProcessOptiTrackFrame;
                //robot.Uninitialize();

                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ProcessRobotFrame(object sender, KUKA.FrameReceivedEventArgs args) {
            lock (syncLock) {
                if (robotMovedToHitPosition && robot.IsTargetPositionReached) {
                    // Slow down the robot
                    regB.Shift();
                    regC.Shift();

                    robotMovedToHitPosition = false;
                    robot.MoveTo(new RobotVector(0.44, 850.57, 170.71, 0.0, 0.0, -90.0), RobotVector.Zero, 5);
                    //Stop(); // comment if 194 is commented

                    //robot.FrameReceived -= ProcessRobotFrame;
                    prediction.Reset(prediction.TargetHitHeight);
                    Console.WriteLine("END: " + elapsedTime);
                    elapsedTime = 0;
                    koniec_odbicia = true;
                    Console.WriteLine("***** ***");
                }
            }
        }

        private void ProcessOptiTrackFrame(object sender, OptiTrack.FrameReceivedEventArgs args) {
            Vector<double> robotBaseBallPosition = robot.OptiTrackTransformation.Convert(args.BallPosition);

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
                    elapsedTime += args.ReceivedFrame.DeltaTime;
                }

                if (robotBaseBallPosition[0] == prevBallPos[0] && robotBaseBallPosition[1] == prevBallPos[1] & robotBaseBallPosition[2] == prevBallPos[2]) {
                    prevBallPos = robotBaseBallPosition;
                    return;
                }

                prevBallPos = robotBaseBallPosition;

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

                /*upVector = Vector<double>.Build.DenseOfArray(new double[] {
                            (destBallPosition[0] - predBallPosition[0]) / t,
                            (destBallPosition[1] - predBallPosition[1]) / t,
                            Math.Sqrt(2.0 * 9.81 * 1000 * (maxBallHeigth - predBallPosition[2]))
                        });*/

                var racketNormalVector = upVector.Normalize(1.0) - predBallVelocity.Normalize(1.0);
                Console.WriteLine("Up: " + upVector.Normalize(1.0) + " -ballvel: "  + predBallVelocity.Normalize(1.0) + " = " + racketNormalVector);

                double angleB = Math.Atan2(racketNormalVector[0], racketNormalVector[2]) * 180.0 / Math.PI;
                double angleC = -90.0 - Math.Atan2(racketNormalVector[1], racketNormalVector[2]) * 180.0 / Math.PI;
                //angleB += regB.ComputeU(predBallPosition[0], elapsedTime + predTimeToHit);
                //angleC -= regC.ComputeU(predBallPosition[1], elapsedTime + predTimeToHit);
                //Console.WriteLine("regB: " + angleB + " + " + regB.ComputeU(predBallPosition[0], 0.004));
                //Console.WriteLine("regC: " + angleC + " + " + regC.ComputeU(predBallPosition[1], 0.004));
                angleB = Math.Min(Math.Max(angleB, -20.0), 20.0);
                angleC = Math.Min(Math.Max(angleC, -110.0), -70.0);

                // NIE LEGITNE
                var robotTargetVelocity = new RobotVector(0, 0, 0
                /*(upVector[0] + CoR * predBallVelocity[0]) / (1 + CoR),
                (upVector[1] + CoR * predBallVelocity[1]) / (1 + CoR),
                (upVector[2] + CoR * predBallVelocity[2]) / (1 + CoR)*/
                );
                
                var normalProjection = Projection(racketNormalVector);
                double speed = (Norm(normalProjection * upVector) - Norm(normalProjection * predBallVelocity) * CoR) / (1.0 + CoR);
                //Console.WriteLine("Speed: " + speed);

                double dampCoeff = 1;

                if (predTimeToHit >= 0.4 && robot.Limits.CheckPosition(new RobotVector(predBallPosition[0], predBallPosition[1], predBallPosition[2], 0, angleB, angleC))) {
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

                if (robot.Limits.CheckPosition(robotTargetPostion)) {
                    lock (syncLock) {
                        robotMovedToHitPosition = true;
                        if (!koniec_odbicia || 1==1) {
                            racketNormalVector = racketNormalVector.Normalize(1.0);
                            robotTargetVelocity = new RobotVector(racketNormalVector[0] * 0, racketNormalVector[1] * 0, 180);
                            if (robot.IsInitialized()) {
                                robot.MoveTo(robotTargetPostion, robotTargetVelocity, 2);
                            }
                        } else {
                            //robot.MoveTo(robotTargetPostion, new RobotVector(0, 0, 0), predTimeToHit);
                        }
                        /*Console.WriteLine("vr: " + robotTargetPostion);
                        Console.WriteLine("vp: " + upVector);
                        Console.WriteLine("Speed: " + speed);*/
                    }
                }
            }

            if (prediction.TimeToHit < 0.3 && prediction.TimeToHit != -1 && prediction.TimeToHit != -20) {
                koniec_odbicia = true;
            }

            PingDataReadyEventArgs data = new PingDataReadyEventArgs {
                PredictedBallPosition = predBallPosition,
                PredictedBallVelocity = predBallVelocity,
                ActualBallPosition = robotBaseBallPosition,
                ActualRobotPosition = robotActualPosition,
                PredictedTimeToHit = predTimeToHit,
                BallSetpointX = destBallPosition[0],
                BallSetpointY = destBallPosition[1],
                BallSetpointZ = destBallPosition[2]
            };

            DataReady?.Invoke(this, data);


        }

        public bool IsStarted() {
            return isStarted;
        }

        private Matrix<double> Projection(Vector<double> vec) {
            var mat = Matrix<double>.Build.Dense(vec.Count, vec.Count);
            double denom = 0.0;

            for (int i = 0; i < vec.Count; i++) {
                for(int j = 0; j < vec.Count; j++) {
                    mat[i, j] = vec[i] * vec[j];
                }
                denom += vec[i] * vec[i];
            }

            mat /= denom;
            return mat;
        }

        private double Norm(Vector<double> vec) {
            double n = 0.0;
            for (int i = 0; i < vec.Count; i++) {
                n += vec[i] * vec[i];
            }
            return Math.Sqrt(n);
        }
    }
}