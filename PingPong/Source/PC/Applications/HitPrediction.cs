using MathNet.Numerics.LinearAlgebra;
using PingPong.Maths;
using System;
using System.Collections.Generic;

namespace PingPong.Applications {
    public class HitPrediction {

        private class BallModel : KalmanModel {
            public BallModel() {
                double ts = 0.004;

                F = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 1, ts, ts * ts / 2.0 },
                    { 0, 1, ts },
                    { 0, 0, 1 }
                });

                B = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 0 },
                    { 0 },
                    { 0 }
                });

                H = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 1, 0, 0 }
                });

                Q = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 0.15, 0, 0 },
                    { 0, 1000, 0 },
                    { 0, 0, 1000 }
                });

                R = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 1 }
                });
            }
        }

        private readonly double timeErrorTolerance = 0.03;

        private readonly int timeCheckRange = 5;

        private readonly int maxPolyfitSamples = 50;

        private readonly KalmanFilter kalmanX, kalmanY, kalmanZ;

        private readonly Polyfit polyfitX, polyfitY, polyfitZ;

        private readonly List<double> predictedTimeSamples;

        public Vector<double> Position { get; private set;}

        public Vector<double> Velocity { get; private set; }

        public double TargetHitHeight { get; private set; }

        public double TimeToHit { get; private set; }

        public bool IsReady { get; private set; }

        public int SamplesCount { get; private set; }

        public HitPrediction() {
            polyfitX = new Polyfit(1);
            polyfitY = new Polyfit(1);
            polyfitZ = new Polyfit(2);

            KalmanModel ballModel = new BallModel();
            kalmanX = new KalmanFilter(ballModel);
            kalmanY = new KalmanFilter(ballModel);
            kalmanZ = new KalmanFilter(ballModel);

            predictedTimeSamples = new List<double>();
            Position = Vector<double>.Build.Dense(3);
            Velocity = Vector<double>.Build.Dense(3);
        }

        public void AddMeasurement(Vector<double> position, double elapsedTime) {
            if (polyfitZ.Values.Count == maxPolyfitSamples) {
                ShiftPolyfitSamples();
            }

            polyfitX.AddPoint(elapsedTime, position[0]);
            polyfitY.AddPoint(elapsedTime, position[1]);
            polyfitZ.AddPoint(elapsedTime, position[2]);

            kalmanX.Compute(position[0]);
            kalmanY.Compute(position[1]);
            kalmanZ.Compute(position[2]);

            if (SamplesCount < 25) { //TODO: DO TESTOWANIA - KIEDY MA WYSTARTOWAC
                IsReady = false;
                SamplesCount++;
                return;
            }

            double positionX = 0;
            double positionY = 0;
            double positionZ = 0;

            double velocityX = 0;
            double velocityY = 0;
            double velocityZ = 0;

            if (SamplesCount < 60) { //TODO: DO TESTOWANIA - KIEDY MA DZIALAC POLYFIT
                double z = kalmanZ.CorrectedState[0];
                double v = kalmanZ.CorrectedState[1];
                double a = -9.81 * 1000.0 / 2.0;
                double delta = v * v - 4.0 * a * (z - TargetHitHeight + 20);

                if (delta < 0.0) {
                    TimeToHit = -1.0;
                } else {
                    TimeToHit = (-v - Math.Sqrt(delta)) / (2.0 * a);

                    positionX = kalmanX.CorrectedState[1] * TimeToHit + kalmanX.CorrectedState[0];
                    positionY = kalmanY.CorrectedState[1] * TimeToHit + kalmanY.CorrectedState[0];
                    positionZ = TargetHitHeight;

                    velocityX = kalmanX.CorrectedState[1];
                    velocityY = kalmanZ.CorrectedState[1];
                    velocityZ = 2.0 * a * TimeToHit + v;
                }
            } else {
                var zCoeffs = polyfitZ.CalculateCoefficients();

                double z0 = zCoeffs[0];
                double v0 = zCoeffs[1];
                double a0 = zCoeffs[2];
                double delta = v0 * v0 - 4.0 * a0 * (z0 - TargetHitHeight + 20);

                if (delta < 0.0) {
                    TimeToHit = -1.0;
                } else {
                    double timeOfFlight = (-v0 - Math.Sqrt(delta)) / (2.0 * a0);
                    TimeToHit = timeOfFlight - elapsedTime;

                    var xCoeffs = polyfitX.CalculateCoefficients();
                    var yCoeffs = polyfitY.CalculateCoefficients();

                    positionX = xCoeffs[1] * timeOfFlight + xCoeffs[0];
                    positionY = yCoeffs[1] * timeOfFlight + yCoeffs[0];
                    positionZ = TargetHitHeight;

                    velocityX = xCoeffs[1];
                    velocityY = yCoeffs[1];
                    velocityZ = 2.0 * zCoeffs[2] * timeOfFlight + zCoeffs[1];
                }
            }

            Position = Vector<double>.Build.DenseOfArray(new double[] {
                        positionX, positionY, positionZ
                    });

            Velocity = Vector<double>.Build.DenseOfArray(new double[] {
                velocityX, velocityY, velocityZ
            });

            SamplesCount++;
            IsReady = TimeToHit > 0;
        }

        public void Reset(double targetHitHeight) {
            TargetHitHeight = targetHitHeight;

            polyfitX.Values.Clear();
            polyfitY.Values.Clear();
            polyfitZ.Values.Clear();

            kalmanX.Reset();
            kalmanY.Reset();
            kalmanZ.Reset();

            IsReady = false;
            SamplesCount = 0;
            TimeToHit = -20.0;
        }

        private void ShiftPolyfitSamples() {
            for (int i = 0; i < maxPolyfitSamples / 2; i++) {
                polyfitX.Values[i] = polyfitX.Values[2 * i];
                polyfitY.Values[i] = polyfitY.Values[2 * i];
                polyfitZ.Values[i] = polyfitZ.Values[2 * i];
            }

            polyfitX.Values.RemoveRange(maxPolyfitSamples / 2, maxPolyfitSamples / 2);
            polyfitY.Values.RemoveRange(maxPolyfitSamples / 2, maxPolyfitSamples / 2);
            polyfitZ.Values.RemoveRange(maxPolyfitSamples / 2, maxPolyfitSamples / 2);

        }

        private double CalculatePredictedTime(double z0, double v0, double a0) {
            // z(t) = a0 * t^2 + v0 * t + z0
            // z(T) = a0 * T^2 + v0 * T + z0 = TargetHitHeight
            // T = predicted time of flight

            if (a0 >= 0.0) { // negative acceleration expected (-g/2)
                return -1.0;
            }

            double delta = v0 * v0 - 4.0 * a0 * (z0 - TargetHitHeight);

            if (delta < 0.0) { // no real roots
                return -1.0;
            } else {
                return (-v0 - Math.Sqrt(delta)) / (2.0 * a0);
            }
        }

        private bool IsPredictedTimeStable(double predictedTime) {
            if (predictedTimeSamples.Count == timeCheckRange) {
                predictedTimeSamples.RemoveAt(0);
                predictedTimeSamples.Add(predictedTime);

                bool isTimeStable = true;

                for (int i = 1; i < predictedTimeSamples.Count; i++) {
                    isTimeStable &= Math.Abs(predictedTimeSamples[i] - predictedTimeSamples[i - 1]) <= timeErrorTolerance;

                    if (!isTimeStable) {
                        return false;
                    }
                }

                return isTimeStable;
            } else {
                predictedTimeSamples.Add(predictedTime);

                return false;
            }
        }

    }
}

