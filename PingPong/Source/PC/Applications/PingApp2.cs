using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.Applications {
    class PingApp2 : IApplication<PingDataReadyEventArgs> {

        private readonly Robot robot;

        private readonly OptiTrackSystem optiTrack;

        private readonly HitPrediction prediction;

        private readonly Func<Vector<double>, bool> checkFunction;

        private readonly Vector<double> upVector = Vector<double>.Build.DenseOfArray(new double[] { 0, 0, 1 });

        private readonly int bufferSize = 5;

        private readonly List<(double FrameDeltaTime, Vector<double> Position)> samplesBuffer;

        private readonly PIRegulator regB;

        private readonly PIRegulator regC;

        private bool isStarted;

        private double elapsedTime;

        private bool weAreWaitingForBallToHit = false;

        private Vector<double> prevBallPosition;

        private Vector<double> currentBallPosition;

        public event EventHandler Started;

        public event EventHandler Stopped;

        public event EventHandler<PingDataReadyEventArgs> DataReady;

        public PingApp2(Robot robot, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot = robot;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;

            regB = new PIRegulator(0.005, 0.001, 0.004, 0.44);
            regC = new PIRegulator(0.005, 0.001, 0.004, 850.71);

            samplesBuffer = new List<(double, Vector<double>)>();
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
                optiTrack.FrameReceived += ProcessOptiTrackFrame;
                Started?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Stop() {
            if (isStarted) {
                isStarted = false;

                optiTrack.FrameReceived -= ProcessOptiTrackFrame;
                robot.Uninitialize();

                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ProcessOptiTrackFrame(object sender, OptiTrack.FrameReceivedEventArgs args) {
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

            if (weAreWaitingForBallToHit) {
                if (samplesBuffer.Count == bufferSize) {
                    samplesBuffer.Add((args.ReceivedFrame.DeltaTime, currentBallPosition));
                    samplesBuffer.RemoveAt(0);
                } else {
                    samplesBuffer.Add((args.ReceivedFrame.DeltaTime, currentBallPosition));
                    return;
                }

                prediction.AddMeasurement(samplesBuffer[0].Position, 0);

                for (int i = 1; i < samplesBuffer.Count; i++) {
                    elapsedTime += samplesBuffer[i].FrameDeltaTime;
                    prediction.AddMeasurement(samplesBuffer[i].Position, elapsedTime);

                    if (samplesBuffer[i].Position[2] <= samplesBuffer[i - 1].Position[2]) {
                        prediction.Reset(180);
                        elapsedTime = 0;
                        return;
                    }
                }

                samplesBuffer.Clear();
                weAreWaitingForBallToHit = false;
            }

            if (prediction.SamplesCount != 0) {
                elapsedTime += args.ReceivedFrame.DeltaTime;
            }

            prediction.AddMeasurement(currentBallPosition, elapsedTime);
            RobotVector robotActualPosition = robot.Position;

            if (prediction.IsReady) {
                if (prediction.TimeToHit >= 0.25) {
                    var paddleNormalVector = upVector.Normalize(1.0) - prediction.Velocity.Normalize(1.0);
                    Console.WriteLine("Up: " + upVector.Normalize(1.0) + " -ballvel: " + prediction.Velocity.Normalize(1.0) + " = " + paddleNormalVector);

                    double angleB = Math.Atan2(paddleNormalVector[0], paddleNormalVector[2]) * 180.0 / Math.PI;
                    double angleC = -90.0 - Math.Atan2(paddleNormalVector[1], paddleNormalVector[2]) * 180.0 / Math.PI;

                    angleB += regB.ComputeU(prediction.Position[0]);
                    angleC -= regC.ComputeU(prediction.Position[1]);
                    angleB = Math.Min(Math.Max(angleB, -20.0), 20.0);
                    angleC = Math.Min(Math.Max(angleC, -110.0), -70.0);

                    double dampCoeff = 1;

                    if (prediction.TimeToHit >= 0.4) { //TODO: dopisac warunek
                        dampCoeff = Math.Exp(-(prediction.TimeToHit - 0.4) / 0.15);
                    }

                    RobotVector robotTargetPosition = new RobotVector(
                        robotActualPosition.X + (prediction.Position[0] - robotActualPosition.X) * dampCoeff,
                        robotActualPosition.Y + (prediction.Position[1] - robotActualPosition.Y) * dampCoeff,
                        prediction.Position[2],
                        0,
                        robotActualPosition.B + (angleB - robotActualPosition.B) * dampCoeff,
                        robotActualPosition.C + (angleC - robotActualPosition.C) * dampCoeff
                    );


                    RobotVector robotTargetVelocity = new RobotVector(0, 0, 200);

                    if (robot.Limits.CheckMove(robotTargetPosition, robotTargetVelocity, prediction.TimeToHit)) {
                        RobotMovement movement1 = new RobotMovement(robotTargetPosition, robotTargetVelocity, prediction.TimeToHit);
                        RobotMovement movement2 = new RobotMovement(robot.HomePosition, RobotVector.Zero, 1.0);

                        robot.MoveTo(new RobotMovement[] { movement1, movement2 });
                    }
                } else {
                    regB.Shift();
                    regC.Shift();
                    weAreWaitingForBallToHit = true;
                    prediction.Reset(180);
                    elapsedTime = 0;
                }
            }

            PingDataReadyEventArgs data = new PingDataReadyEventArgs {
                PredictedBallPosition = prediction.Position,
                PredictedBallVelocity = prediction.Velocity,
                ActualBallPosition = currentBallPosition,
                ActualRobotPosition = robotActualPosition,
                PredictedTimeToHit = prediction.TimeToHit,
                BallSetpointX = 0,  //TODO:
                BallSetpointY = 0,  //TODO:
                BallSetpointZ = 0   //TODO:
            };

            DataReady?.Invoke(this, data);
        }

    }
}