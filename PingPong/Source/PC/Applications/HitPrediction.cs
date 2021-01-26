﻿using MathNet.Numerics.LinearAlgebra;
using PingPong.Maths;
using System;
using System.Collections.Generic;

namespace PingPong.Applications {
    public class HitPrediction {

        private readonly double timeErrorTolerance = 0.03;

        private readonly int timeCheckRange = 5;

        private readonly int maxPolyfitSamples = 50;

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

            if (SamplesCount < 30) { //TODO: DO TESTOWANIA - KIEDY MA WYSTARTOWAC
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

            var zCoeffs = polyfitZ.CalculateCoefficients();
            double timeOfFlight = CalculatePredictedTime(zCoeffs[0], zCoeffs[1], zCoeffs[2], TargetHitHeight);
            
            if (timeOfFlight > 0.0 && IsPredictedTimeStable(timeOfFlight)) {
                TimeToHit = timeOfFlight - elapsedTime;
                IsReady = true;

                var xCoeffs = polyfitX.CalculateCoefficients();
                var yCoeffs = polyfitY.CalculateCoefficients();

                positionX = xCoeffs[1] * timeOfFlight + xCoeffs[0];
                positionY = yCoeffs[1] * timeOfFlight + yCoeffs[0];
                positionZ = TargetHitHeight;

                velocityX = xCoeffs[1];
                velocityY = yCoeffs[1];
                velocityZ = 2.0 * zCoeffs[2] * timeOfFlight + zCoeffs[1];
            } else {
                TimeToHit = -1;
                IsReady = false;
            }

            Position = Vector<double>.Build.DenseOfArray(new double[] {
                positionX, positionY, positionZ
            });

            Velocity = Vector<double>.Build.DenseOfArray(new double[] {
                velocityX, velocityY, velocityZ
            });

            SamplesCount++;
        }

        public void Reset(double targetHitHeight) {
            TargetHitHeight = targetHitHeight;

            polyfitX.Values.Clear();
            polyfitY.Values.Clear();
            polyfitZ.Values.Clear();

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

        private double CalculatePredictedTime(double z0, double v0, double a0, double z1) {
            // z(t) = a0 * t^2 + v0 * t + z0
            // z(T) = a0 * T^2 + v0 * T + z0 = TargetHitHeight
            // T = predicted time of flight

            if (a0 >= 0.0) { // negative acceleration expected (-g/2)
                return -1.0;
            }

            double delta = v0 * v0 - 4.0 * a0 * (z0 - z1);

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