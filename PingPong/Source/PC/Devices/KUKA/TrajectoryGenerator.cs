using System;
using System.Collections.Generic;

namespace PingPong.KUKA {
    class TrajectoryGenerator {

        private class Polynominal {

            private double k0, k1, k2, k3, k4, k5; // Polynominal coefficients

            private double xn, vn, an, jn; // next value, velocity acceleration and jerk

            /// <summary>
            /// Current (theoretical) position
            /// </summary>
            public double X { get; private set; }

            /// <summary>
            /// Current (theoretical) velocity
            /// </summary>
            public double V { get; private set; }

            /// <summary>
            /// Current (theoretical) acceleration
            /// </summary>
            public double A { get; private set; }

            /// <summary>
            /// Current (theoretical) jerk
            /// </summary>
            public double J { get; private set; }

            public void Initialize(double currentX) {
                k0 = xn = X = currentX;
            }

            public void UpdateCoefficients(double x1, double v1, double T, double elapsedTime) {
                double T1 = T;
                double T2 = T1 * T1;
                double T3 = T1 * T2;
                double T4 = T1 * T3;
                double T5 = T1 * T4;

                double t1 = elapsedTime;
                double t2 = t1 * t1;
                double t3 = t1 * t2;
                double t4 = t1 * t3;
                double t5 = t1 * t4;

                double x0 = k5 * t5 + k4 * t4 + k3 * t3 + k2 * t2 + k1 * t1 + k0;
                vn = 5.0 * k5 * t4 + 4.0 * k4 * t3 + 3.0 * k3 * t2 + 2.0 * k2 * t1 + k1;
                an = 20.0 * k5 * t3 + 12.0 * k4 * t2 + 6.0 * k3 * t1 + 2.0 * k2;
                jn = 60.0 * k5 * t2 + 24.0 * k4 * t1 + 6.0 * k3;

                k0 = x0;
                k1 = vn;
                k2 = an / 2.0;
                k3 = 1.0 / (2.0 * T3) * (-3.0 * T2 * an - 12.0 * T1 * vn - 8.0 * T1 * v1 + 20.0 * (x1 - x0));
                k4 = 1.0 / (2.0 * T4) * (3.0 * T2 * an + 16.0 * T1 * vn + 14.0 * T1 * v1 - 30.0 * (x1 - x0));
                k5 = 1.0 / (2.0 * T5) * (-T2 * an - 6.0 * T1 * (vn + v1) + 12.0 * (x1 - x0));
            }

            public double GetValueAt(double t, bool updateValues = true) {
                double t1 = t;
                double t2 = t1 * t1;
                double t3 = t1 * t2;
                double t4 = t1 * t3;
                double t5 = t1 * t4;

                if (updateValues) {
                    UpdateTheoreticalValues();
                }

                xn = k5 * t5 + k4 * t4 + k3 * t3 + k2 * t2 + k1 * t1 + k0;
                vn = 5.0 * k5 * t4 + 4.0 * k4 * t3 + 3.0 * k3 * t2 + 2.0 * k2 * t1 + k1;
                an = 20.0 * k5 * t3 + 12.0 * k4 * t2 + 6.0 * k3 * t1 + 2.0 * k2;
                jn = 60.0 * k5 * t2 + 24.0 * k4 * t1 + 6.0 * k3;

                return xn;
            }

            public void UpdateTheoreticalValues() {
                X = xn;
                V = vn;
                A = an;
                J = jn;
            }

        }

        private const double Ts = 0.004;

        private readonly object syncLock = new object();

        private readonly Polynominal polyX = new Polynominal();

        private readonly Polynominal polyY = new Polynominal();

        private readonly Polynominal polyZ = new Polynominal();

        private readonly Polynominal polyA = new Polynominal();

        private readonly Polynominal polyB = new Polynominal();

        private readonly Polynominal polyC = new Polynominal();

        private readonly List<RobotMovement> movementsStack = new List<RobotMovement>();

        private RobotMovement currentMovement;

        private double elapsedTime;

        private bool targetPositionReached;

        public RobotVector TargetPosition {
            get {
                lock (syncLock) {
                    return currentMovement.TargetPosition;
                }
            }
        }

        public RobotVector TargetVelocity {
            get {
                lock (syncLock) {
                    return currentMovement.TargetVelocity;
                }
            }
        }

        public double TargetDuration {
            get {
                lock (syncLock) {
                    return currentMovement.TargetDuration;
                }
            }
        }

        public bool IsTargetPositionReached {
            get {
                lock (syncLock) {
                    return targetPositionReached;
                }
            }
        }

        /// <summary>
        /// Current (theoretical) position
        /// </summary>
        public RobotVector Position {
            get {
                lock (syncLock) {
                    return new RobotVector(polyX.X, polyY.X, polyZ.X, polyA.X, polyB.X, polyC.X);
                }
            }
        }

        /// <summary>
        /// Current (theoretical) velocity
        /// </summary>
        public RobotVector Velocity {
            get {
                lock (syncLock) {
                    return new RobotVector(polyX.V, polyY.V, polyZ.V, polyA.V, polyB.V, polyC.V);
                }
            }
        }

        /// <summary>
        /// Current (theoretical) acceleration
        /// </summary>
        public RobotVector Acceleration {
            get {
                lock (syncLock) {
                    return new RobotVector(polyX.A, polyY.A, polyZ.A, polyA.A, polyB.A, polyC.A);
                }
            }
        }

        /// <summary>
        /// Current (theoretical) acceleration
        /// </summary>
        public RobotVector Jerk {
            get {
                lock (syncLock) {
                    return new RobotVector(polyX.J, polyY.J, polyZ.J, polyA.J, polyB.J, polyC.J);
                }
            }
        }

        public TrajectoryGenerator() {
        }

        public void Initialize(RobotVector actualRobotPosition) {
            lock (syncLock) {
                targetPositionReached = true;
                elapsedTime = 0.0;
                currentMovement = new RobotMovement(actualRobotPosition, RobotVector.Zero, -1.0);

                polyX.Initialize(actualRobotPosition.X);
                polyY.Initialize(actualRobotPosition.Y);
                polyZ.Initialize(actualRobotPosition.Z);
                polyA.Initialize(actualRobotPosition.A);
                polyB.Initialize(actualRobotPosition.B);
                polyC.Initialize(actualRobotPosition.C);
            }
        }

        private void UpdateCurrentMovement(RobotMovement movement) {
            movement = new RobotMovement(
                movement.TargetPosition,
                movement.TargetVelocity,
                movement.TargetDuration - 0.032
            );

            RobotVector targetPosition = movement.TargetPosition;
            RobotVector targetVelocity = movement.TargetVelocity;
            double targetDuration = movement.TargetDuration;

            if (movement.TargetDuration <= 0.0) {
                throw new ArgumentException($"Duration value must be greater than 0.032s");
            }

            bool targetPositionChanged = !targetPosition.Compare(currentMovement.TargetPosition, 1, 0.1);
            bool targetVelocityChanged = !targetVelocity.Compare(currentMovement.TargetVelocity, 1, 0.1);
            bool targetDurationChanged = Math.Abs(targetDuration - currentMovement.TargetDuration) >= 0.001;

            if (targetDurationChanged || targetPositionChanged || targetVelocityChanged) {
                double tmpElapsedTime = elapsedTime;

                currentMovement = movement;
                targetPositionReached = false;
                elapsedTime = 0.0;

                polyX.UpdateCoefficients(targetPosition.X, targetVelocity.X, targetDuration, tmpElapsedTime);
                polyY.UpdateCoefficients(targetPosition.Y, targetVelocity.Y, targetDuration, tmpElapsedTime);
                polyZ.UpdateCoefficients(targetPosition.Z, targetVelocity.Z, targetDuration, tmpElapsedTime);
                polyA.UpdateCoefficients(targetPosition.A, targetVelocity.A, targetDuration, tmpElapsedTime);
                polyB.UpdateCoefficients(targetPosition.B, targetVelocity.B, targetDuration, tmpElapsedTime);
                polyC.UpdateCoefficients(targetPosition.C, targetVelocity.C, targetDuration, tmpElapsedTime);
            }
        }

        public void SetMovement(RobotMovement movement) {
            lock (syncLock) {
                movementsStack.Clear();
                UpdateCurrentMovement(movement);
            }
        }

        public void SetMovementsStack(RobotMovement[] movements) {
            if (movements.Length == 0) {
                throw new ArgumentException("Movements array must be not empty");
            }

            lock (syncLock) {
                movementsStack.Clear();

                for (int i = 1; i < movements.Length; i++) {
                    movementsStack.Add(movements[i]);
                }

                UpdateCurrentMovement(movements[0]);
            }
        }

        public RobotVector GetNextCorrection() {
            lock (syncLock) {
                if (targetPositionReached) {
                    return RobotVector.Zero;
                }

                if (elapsedTime < currentMovement.TargetDuration) {
                    elapsedTime += Ts;

                    double nx = polyX.GetValueAt(elapsedTime);
                    double ny = polyY.GetValueAt(elapsedTime);
                    double nz = polyZ.GetValueAt(elapsedTime);
                    double na = polyA.GetValueAt(elapsedTime);
                    double nb = polyB.GetValueAt(elapsedTime);
                    double nc = polyC.GetValueAt(elapsedTime);
                    
                    return new RobotVector(nx, ny, nz, na, nb, nc) - Position;
                } else {
                    polyX.UpdateTheoreticalValues();
                    polyY.UpdateTheoreticalValues();
                    polyZ.UpdateTheoreticalValues();
                    polyA.UpdateTheoreticalValues();
                    polyB.UpdateTheoreticalValues();
                    polyC.UpdateTheoreticalValues();

                    if (movementsStack.Count != 0) {
                        RobotMovement nextMovement = movementsStack[0];
                        UpdateCurrentMovement(nextMovement);
                        movementsStack.RemoveAt(0);

                        elapsedTime = Ts;

                        double nx = polyX.GetValueAt(elapsedTime);
                        double ny = polyY.GetValueAt(elapsedTime);
                        double nz = polyZ.GetValueAt(elapsedTime);
                        double na = polyA.GetValueAt(elapsedTime);
                        double nb = polyB.GetValueAt(elapsedTime);
                        double nc = polyC.GetValueAt(elapsedTime);

                        return new RobotVector(nx, ny, nz, na, nb, nc) - Position;
                    } else {
                        targetPositionReached = true;
                        return RobotVector.Zero;
                    }
                }
            }
        }

    }
}