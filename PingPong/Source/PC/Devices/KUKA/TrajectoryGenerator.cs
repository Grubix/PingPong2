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

            public void UpdateCoefficients(double x0, double x1, double v0, double v1, double a0, double T) {
                double T1 = T;
                double T2 = T1 * T1;
                double T3 = T1 * T2;
                double T4 = T1 * T3;
                double T5 = T1 * T4;

                k0 = x0;
                k1 = v0;
                k2 = a0 / 2.0;
                k3 = 1.0 / (2.0 * T3) * (-3.0 * T2 * a0 - 12.0 * T1 * v0 - 8.0 * T1 * v1 + 20.0 * (x1 - x0));
                k4 = 1.0 / (2.0 * T4) * (3.0 * T2 * a0 + 16.0 * T1 * v0 + 14.0 * T1 * v1 - 30.0 * (x1 - x0));
                k5 = 1.0 / (2.0 * T5) * (-T2 * a0 - 6.0 * T1 * (v0 + v1) + 12.0 * (x1 - x0));
            }

            public double GetValueAt(double t, bool updateValues = true) {
                double t1 = t;
                double t2 = t1 * t1;
                double t3 = t1 * t2;
                double t4 = t1 * t3;
                double t5 = t1 * t4;

                if (updateValues) {
                    X = xn;
                    V = vn;
                    A = an;
                    J = jn;
                }

                xn = k5 * t5 + k4 * t4 + k3 * t3 + k2 * t2 + k1 * t1 + k0;
                vn = 5.0 * k5 * t4 + 4.0 * k4 * t3 + 3.0 * k3 * t2 + 2.0 * k2 * t1 + k1;
                an = 20.0 * k5 * t3 + 12.0 * k4 * t2 + 6.0 * k3 * t1 + 2.0 * k2;
                jn = 60.0 * k5 * t2 + 24.0 * k4 * t1 + 6.0 * k3;

                return xn;
            }

            public void UpdateTheoreticalValues(double x1, double v1) {
                X = xn;
                V = vn;
                A = an = 0;
                J = jn = 0;
            }

            public void Update() {
                X = xn;
                V = vn;
                A = an = 0;
                J = jn = 0;
            }

        }

        private readonly double Ts = 0.004;

        private readonly Polynominal polyX = new Polynominal();

        private readonly Polynominal polyY = new Polynominal();

        private readonly Polynominal polyZ = new Polynominal();

        private readonly Polynominal polyA = new Polynominal();

        private readonly Polynominal polyB = new Polynominal();

        private readonly Polynominal polyC = new Polynominal();

        private readonly List<RobotMovement> movementsStack = new List<RobotMovement>();

        private RobotMovement currentMovement;

        private double elapsedTime;

        public RobotVector TargetPosition {
            get {
                return currentMovement.TargetPosition;
            }
        }

        public RobotVector TargetVelocity {
            get {
                return currentMovement.TargetVelocity;
            }
        }

        public double TargetDuration {
            get {
                return currentMovement.TargetDuration;
            }
        }

        public bool IsTargetPositionReached { get; private set; }

        /// <summary>
        /// Current (theoretical) position
        /// </summary>
        public RobotVector Position {
            get {
                return new RobotVector(polyX.X, polyY.X, polyZ.X, polyA.X, polyB.X, polyC.X);
            }
        }

        /// <summary>
        /// Current (theoretical) velocity
        /// </summary>
        public RobotVector Velocity {
            get {
                return new RobotVector(polyX.V, polyY.V, polyZ.V, polyA.V, polyB.V, polyC.V);
            }
        }

        /// <summary>
        /// Current (theoretical) acceleration
        /// </summary>
        public RobotVector Acceleration {
            get {
                return new RobotVector(polyX.A, polyY.A, polyZ.A, polyA.A, polyB.A, polyC.A);
            }
        }

        /// <summary>
        /// Current (theoretical) acceleration
        /// </summary>
        public RobotVector Jerk {
            get {
                return new RobotVector(polyX.J, polyY.J, polyZ.J, polyA.J, polyB.J, polyC.J);
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
                currentMovement = movement;
                elapsedTime = 0.0;
                IsTargetPositionReached = false;

                polyX.UpdateCoefficients(Position.X, targetPosition.X, Velocity.X, targetVelocity.X, Acceleration.X, targetDuration);
                polyY.UpdateCoefficients(Position.Y, targetPosition.Y, Velocity.Y, targetVelocity.Y, Acceleration.Y, targetDuration);
                polyZ.UpdateCoefficients(Position.Z, targetPosition.Z, Velocity.Z, targetVelocity.Z, Acceleration.Z, targetDuration);
                polyA.UpdateCoefficients(Position.A, targetPosition.A, Velocity.A, targetVelocity.A, Acceleration.A, targetDuration);
                polyB.UpdateCoefficients(Position.B, targetPosition.B, Velocity.B, targetVelocity.B, Acceleration.B, targetDuration);
                polyC.UpdateCoefficients(Position.C, targetPosition.C, Velocity.C, targetVelocity.C, Acceleration.C, targetDuration);
            }
        }

        public void Initialize(RobotVector actualRobotPosition) {
            elapsedTime = 0.0;
            IsTargetPositionReached = true;
            currentMovement = new RobotMovement(actualRobotPosition, RobotVector.Zero, -1.0);

            polyX.Initialize(actualRobotPosition.X);
            polyY.Initialize(actualRobotPosition.Y);
            polyZ.Initialize(actualRobotPosition.Z);
            polyA.Initialize(actualRobotPosition.A);
            polyB.Initialize(actualRobotPosition.B);
            polyC.Initialize(actualRobotPosition.C);
        }

        public void SetMovement(RobotMovement movement) {
            movementsStack.Clear();
            UpdateCurrentMovement(movement);
        }

        public void SetMovements(RobotMovement[] movements) {
            if (movements.Length == 0) {
                throw new ArgumentException("Movements array must be not empty");
            }

            movementsStack.Clear();

            for (int i = 1; i < movements.Length; i++) {
                movementsStack.Add(movements[i]);
            }

            UpdateCurrentMovement(movements[0]);
        }

        private double diff;

        public RobotVector GetNextCorrection() {
            if (IsTargetPositionReached) {
                return RobotVector.Zero;
            }

            if (elapsedTime < currentMovement.TargetDuration) {
                elapsedTime += Ts;

                double nx, ny, nz, na, nb, nc;

                if (elapsedTime >= currentMovement.TargetDuration) {
                    nx = polyX.GetValueAt(currentMovement.TargetDuration);
                    ny = polyY.GetValueAt(currentMovement.TargetDuration);
                    nz = polyZ.GetValueAt(currentMovement.TargetDuration);
                    na = polyA.GetValueAt(currentMovement.TargetDuration);
                    nb = polyB.GetValueAt(currentMovement.TargetDuration);
                    nc = polyC.GetValueAt(currentMovement.TargetDuration);

                    diff = elapsedTime - currentMovement.TargetDuration - Ts;
                } else {
                    nx = polyX.GetValueAt(elapsedTime);
                    ny = polyY.GetValueAt(elapsedTime);
                    nz = polyZ.GetValueAt(elapsedTime);
                    na = polyA.GetValueAt(elapsedTime);
                    nb = polyB.GetValueAt(elapsedTime);
                    nc = polyC.GetValueAt(elapsedTime);
                }

                return new RobotVector(nx, ny, nz, na, nb, nc) - Position;
            } else {
                RobotVector tartgetPosition = currentMovement.TargetPosition;
                RobotVector targetVelocity = currentMovement.TargetVelocity;

                polyX.UpdateTheoreticalValues(tartgetPosition.X, targetVelocity.X);
                polyY.UpdateTheoreticalValues(tartgetPosition.Y, targetVelocity.Y);
                polyZ.UpdateTheoreticalValues(tartgetPosition.Z, targetVelocity.Z);
                polyA.UpdateTheoreticalValues(tartgetPosition.A, targetVelocity.A);
                polyB.UpdateTheoreticalValues(tartgetPosition.B, targetVelocity.B);
                polyC.UpdateTheoreticalValues(tartgetPosition.C, targetVelocity.C);

                if (movementsStack.Count != 0) {
                    //TODO: KONIECZNIE SPRAWDZENIE CZY RUCH SIE ZMIENIL - JAK PODA SIE DWA TE SAME KOREKCJA Z DUPY

                    UpdateCurrentMovement(movementsStack[0]);
                    movementsStack.RemoveAt(0);
                    elapsedTime = diff;

                    double nx = polyX.GetValueAt(elapsedTime);
                    double ny = polyY.GetValueAt(elapsedTime);
                    double nz = polyZ.GetValueAt(elapsedTime);
                    double na = polyA.GetValueAt(elapsedTime);
                    double nb = polyB.GetValueAt(elapsedTime);
                    double nc = polyC.GetValueAt(elapsedTime);

                    return new RobotVector(nx, ny, nz, na, nb, nc) - Position;
                } else {
                    IsTargetPositionReached = true;

                    //polyX.UpdateTheoreticalValues(tPos.X, tVel.X);
                    //polyY.UpdateTheoreticalValues(tPos.Y, tVel.Y);
                    //polyZ.UpdateTheoreticalValues(tPos.Z, tVel.Z);
                    //polyA.UpdateTheoreticalValues(tPos.A, tVel.A);
                    //polyB.UpdateTheoreticalValues(tPos.B, tVel.B);
                    //polyC.UpdateTheoreticalValues(tPos.C, tVel.C);

                    return RobotVector.Zero;
                }
            }
        }

    }
}