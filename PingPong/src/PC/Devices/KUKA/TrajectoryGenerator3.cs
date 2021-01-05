using MathNet.Numerics.LinearAlgebra;
using System;

namespace PingPong.KUKA {
    public class TrajectoryGenerator3 {

        private class Parameter {

            private double k0;
            private double k1;
            private double k2;
            private double k3;

            private double nextValue;
            public double V { get; private set; }

            public Parameter() {
                k0 = 0.0;
                k1 = 0.0;
                k2 = 0.0;
                k3 = 0.0;
                V = 0.0;
                nextValue = 0.0;
            }

            public void UpdateCoefficients(double currentPosition, double targetPosition, double targetVelocity, double time) {
                k0 = currentPosition;
                k1 = V;
                k2 = (3 * (targetPosition - currentPosition) - 2 * V * time - targetVelocity * time) / Math.Pow(time, 2);
                k3 = (targetVelocity * time + V * time - 2 * (targetPosition - currentPosition)) / Math.Pow(time, 3);
            }

            public void ComputeNextValue(double period) {
                nextValue = k3 * Math.Pow(period, 3) + k2 * Math.Pow(period, 2) + k1 * period;
            }

            public void UpdateVelocity(double period) {
                V = 3 * k3 * Math.Pow(period, 2) + 2 * k2 * period + k1;
            }

            public double GetNextValue() {
                return nextValue;
            }

            public void ResetVelocity() {
                V = 0.0;
            }
        }

        private readonly Parameter X = new Parameter();
        private readonly Parameter Y = new Parameter();
        private readonly Parameter Z = new Parameter();
        private readonly Parameter A = new Parameter();
        private readonly Parameter B = new Parameter();
        private readonly Parameter C = new Parameter();

        private readonly object syncLock = new object();
        private readonly double period = 0.004;
        private double time2Dest = 0.0;
        private double totalTime2Dest = 0.0;
        private RobotVector targetPosition;

        private bool targetPositionReached = true;

        public bool TargetPositionReached {
            get {
                lock (syncLock) {
                    return targetPositionReached;
                }
            }
        }

        public TrajectoryGenerator3(RobotVector currentPosition) {
            targetPosition = currentPosition;
        }

        public void SetTargetPosition(RobotVector targetPosition, double time) {
            if (time <= 0.0) {
                throw new ArgumentException($"Duration value must be greater than 0, get {time}");
            }

            if (totalTime2Dest != time || !targetPosition.Compare(this.targetPosition, 0.1, 1)) {
                lock (syncLock) {
                    this.targetPosition = targetPosition.Clone() as RobotVector;
                    totalTime2Dest = time;
                    time2Dest = time;
                    targetPositionReached = false;
                }
            }
        }

        public RobotVector GetNextCorrection(RobotVector currentPosition) {
            lock (syncLock) {
                if (time2Dest >= 0.004) {
                    UpdateCoefficients(currentPosition, targetPosition, time2Dest);
                    ComputeNextPoint();
                    time2Dest -= period;
                    UpdateVelocity();

                    return new RobotVector(
                        X.GetNextValue(),
                        Y.GetNextValue(),
                        Z.GetNextValue(),
                        A.GetNextValue(),
                        B.GetNextValue(),
                        C.GetNextValue()
                    );
                } else {
                    targetPositionReached = true;
                    totalTime2Dest = 0.0;
                    ResetVelocity();
                    return new RobotVector();
                }
            }
        }

        // UWAGA: TOTALNIE NIE WIADOMO CZY DZIAŁA   
        // targetZVelocity [mm / s]
        public RobotVector Hit(RobotVector currentPosition, double targetZVelocity) {
            lock (syncLock) {
                if (totalTime2Dest > 0 && time2Dest - 50 / targetZVelocity > 0.004) {
                    // ruch do punktu pod pilka
                    RobotVector underTargetPosition = targetPosition + new RobotVector(0, 0, -50, 0, 0, 0);
                    UpdateCoefficients(currentPosition, underTargetPosition, time2Dest - 50 / targetZVelocity, targetZVelocity);
                    ComputeNextPoint();
                    time2Dest -= period;
                    UpdateVelocity();

                    return new RobotVector(
                        X.GetNextValue(),
                        Y.GetNextValue(),
                        Z.GetNextValue(),
                        A.GetNextValue(),
                        B.GetNextValue(),
                        C.GetNextValue()
                    );
                } else if (time2Dest + 50 / targetZVelocity > 0.004) {
                    // ruch v = const 
                    time2Dest -= period;
                    return new RobotVector(0, 0, targetZVelocity * period, 0, 0, 0);
                } else if (time2Dest + 50 / targetZVelocity + 3 > 0.004) {
                    // wytracenie predkosci w czasie np: 3s, do pozycji odbicia
                    UpdateCoefficients(currentPosition, targetPosition, time2Dest + 50 / targetZVelocity + 3);
                    ComputeNextPoint();
                    time2Dest -= period;
                    UpdateVelocity();

                    return new RobotVector(
                        X.GetNextValue(),
                        Y.GetNextValue(),
                        Z.GetNextValue(),
                        A.GetNextValue(),
                        B.GetNextValue(),
                        C.GetNextValue()
                    );
                } else {
                    // zwroc 0, reset
                    targetPositionReached = true;
                    totalTime2Dest = 0.0;
                    ResetVelocity();
                    return new RobotVector();
                }
            }
        }

        private void UpdateCoefficients(RobotVector currentPosition, RobotVector targetPosition, double time2Dest, double targetZLevel = 0.0) {
            // guessing targetVelocity == 0.0
            X.UpdateCoefficients(currentPosition.X, targetPosition.X, 0.0, time2Dest);
            Y.UpdateCoefficients(currentPosition.Y, targetPosition.Y, 0.0, time2Dest);
            Z.UpdateCoefficients(currentPosition.Z, targetPosition.Z, targetZLevel, time2Dest);

           /* Vector<double> currentABC = currentPosition.ABC;
            Vector<double> targetABC = targetPosition.ABC;

            // handling passing through +-180
            if (targetABC[0] - currentABC[0] > 180.0 || targetABC[0] - currentABC[0] < -180.0) {
                currentABC[0] = (currentABC[0] + 360.0) % 360 - currentABC[0];
                targetABC[0] = (targetABC[0] + 360.0) % 360 - targetABC[0];
            }

            if (targetABC[1] - currentABC[1] > 180.0 || targetABC[1] - currentABC[1] < -180.0) {
                currentABC[1] = (currentABC[1] + 360.0) % 360 - currentABC[1];
                targetABC[1] = (targetABC[1] + 360.0) % 360 - targetABC[1];
            }

            if (targetABC[2] - currentABC[2] > 180.0 || targetABC[2] - currentABC[2] < -180.0) {
                currentABC[2] = (currentABC[2] + 360.0) % 360 - currentABC[2];
                targetABC[2] = (targetABC[2] + 360.0) % 360 - targetABC[2];
            }

            currentPosition += new E6POS(0.0, 0.0, 0.0, currentABC[0], currentABC[1], currentABC[2]);
            targetPosition += new E6POS(0.0, 0.0, 0.0, targetABC[0], targetABC[1], targetABC[2]);*/

            A.UpdateCoefficients(currentPosition.A, targetPosition.A, 0.0, time2Dest);
            B.UpdateCoefficients(currentPosition.B, targetPosition.B, 0.0, time2Dest);
            C.UpdateCoefficients(currentPosition.C, targetPosition.C, 0.0, time2Dest);
        }

        private void ComputeNextPoint() {
            X.ComputeNextValue(period);
            Y.ComputeNextValue(period);
            Z.ComputeNextValue(period);
            A.ComputeNextValue(period);
            B.ComputeNextValue(period);
            C.ComputeNextValue(period);
        }

        private void UpdateVelocity() {
            X.UpdateVelocity(period);
            Y.UpdateVelocity(period);
            Z.UpdateVelocity(period);
            A.UpdateVelocity(period);
            B.UpdateVelocity(period);
            C.UpdateVelocity(period);
        }

        private void ResetVelocity() {
            X.ResetVelocity();
            Y.ResetVelocity();
            Z.ResetVelocity();
            A.ResetVelocity();
            B.ResetVelocity();
            C.ResetVelocity();
        }

        public Vector<double> Velocity {
            get {
                lock (syncLock) {
                    return Vector<double>.Build.DenseOfArray(new double[] {
                        X.V, Y.V, Z.V, A.V, B.V, C.V
                    });
                }
            }
        }

        public Vector<double> Acceleration {
            get {
                lock (syncLock) {
                    return Vector<double>.Build.DenseOfArray(new double[] {
                        0, 0, 0, 0, 0, 0
                    });
                }
            }
        }

    }
}
