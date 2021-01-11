using MathNet.Numerics.LinearAlgebra;
using PingPong.Maths;
using System;
using System.Collections.Generic;

namespace PingPong.Applications {
    public class HitPrediction {

        private readonly double timeErrorTolerance = 0.03;

        private readonly int timeCheckRange = 5;

        private readonly int maxPolyfitSamples = 50;

        private readonly Polyfit polyfitX;

        private readonly Polyfit polyfitY;

        private readonly Polyfit polyfitZ;

        private readonly List<double> predictedTimeSamples;

        public double TargetHitHeight { get; private set; }

        public double TimeOfFlight { get; private set; }

        public double TimeToHit { get; private set; }

        public bool IsReady { get; private set; }

        public int PolyfitSamplesCount {
            get {
                return polyfitZ.Values.Count;
            }
        }

        public List<double> XCoefficients { get; private set; }

        public List<double> YCoefficients { get; private set; }

        public List<double> ZCoefficients { get; private set; }

        public Vector<double> Position { get; private set;}

        public Vector<double> Velocity { get; private set; }

        public HitPrediction() {
            polyfitX = new Polyfit(1);
            polyfitY = new Polyfit(1);
            polyfitZ = new Polyfit(2);
            predictedTimeSamples = new List<double>();
        }

        public void AddMeasurement(Vector<double> position, double elapsedTime) {
            if (PolyfitSamplesCount == maxPolyfitSamples) {
                ShiftPolyfitSamples();
            }

            polyfitX.AddPoint(elapsedTime, position[0]);
            polyfitY.AddPoint(elapsedTime, position[1]);
            polyfitZ.AddPoint(elapsedTime, position[2]);

            TimeOfFlight = CalculatePredictedTimeOfFlight();
            IsReady = TimeOfFlight > 0.1 && IsPredictedTimeStable(TimeOfFlight);
            TimeToHit = IsReady ? TimeOfFlight - elapsedTime : -1.0;
        }

        public void Calculate() {
            XCoefficients = polyfitX.CalculateCoefficients();
            YCoefficients = polyfitY.CalculateCoefficients();
            // ZCoefficients already calculated in CalculatePredictedTimeOfFlight() method

            double positionX = XCoefficients[1] * TimeOfFlight + XCoefficients[0];
            double positionY = YCoefficients[1] * TimeOfFlight + YCoefficients[0];
            double positionZ = TargetHitHeight;

            Position = Vector<double>.Build.DenseOfArray(new double[] {
                positionX, positionY, positionZ
            });

            double velocityX = XCoefficients[1];
            double velocityY = YCoefficients[1];
            double velocityZ = 2.0 * ZCoefficients[2] * TimeOfFlight + ZCoefficients[1];

            Velocity = Vector<double>.Build.DenseOfArray(new double[] {
                velocityX, velocityY, velocityZ
            });
        }

        public void Reset(double targetHitHeight) {
            TargetHitHeight = targetHitHeight;

            polyfitX.Values.Clear();
            polyfitY.Values.Clear();
            polyfitZ.Values.Clear();

            IsReady = false;
            TimeOfFlight = -1.0;
            TimeToHit = -1.0;
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

        private double CalculatePredictedTimeOfFlight() {
            if (polyfitZ.Values.Count < 5) {
                return -1.0;
            }

            ZCoefficients = polyfitZ.CalculateCoefficients();

            // z(t) = a0 * t^2 + v0 * t + z0
            // z(T) = a0 * T^2 + v0 * T + z0 = TargetHitHeight
            // T = predicted time of flight

            double a0 = ZCoefficients[2];
            double v0 = ZCoefficients[1];
            double z0 = ZCoefficients[0];

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