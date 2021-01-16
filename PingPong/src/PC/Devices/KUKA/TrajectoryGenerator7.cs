using System;

namespace PingPong.KUKA {
    class TrajectoryGenerator7 {

        private class Polynominal {

            private double k0, k1, k2, k3, k4, k5, k6, k7; // Polynominal coefficients

            private double xn, vn, an, jn; // Next value, velocity, acceleration and jerk

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

            public double Vp { get; private set; }

            private double prevX0;

            private bool firstMove = true;

            public double GetNextValue(double x0, double x1, double v1, double T1, double t) {

                if (!firstMove) {
                    Vp = (x0 - prevX0) / 0.004;
                    //vn = (3 * vn + Vp) / 4.0;
                }
                prevX0 = x0;
                firstMove = false;

                X = xn;
                V = vn;
                A = an;
                J = jn;

                double T2 = T1 * T1;
                double T3 = T1 * T2;
                double T4 = T1 * T3;
                double T5 = T1 * T4;
                double T6 = T1 * T5;
                double T7 = T1 * T6;

                k0 = x0;
                k1 = vn;
                k2 = an / 2.0;
                k3 = jn / 6;
                k4 = 1.0 / (6.0 * T4) * (-30.0 * T2 * an - 4.0 * T3 * jn - 120.0 * T1 * vn - 90.0 * T1 * v1 + 210.0 * (x1 - x0));
                k5 = 1.0 / (2.0 * T5) * (20.0 * T2 * an + 2.0 * T3 * jn + 90.0 * T1 * vn + 78.0 * T1 * v1 - 168.0 * (x1 - x0));
                k6 = 1.0 / (6.0 * T6) * (-45.0 * T2 * an - 4 * T3 * jn - 216.0 * T1 * vn - 204.0 * T1 * v1 + 420.0 * (x1 - x0));
                k7 = 1.0 / (6.0 * T7) * (12.0 * T2 * an + T3 * jn + 60 * (vn + v1) - 120.0 * (x1 - x0));

                double t1 = t;
                double t2 = t1 * t1;
                double t3 = t1 * t2;
                double t4 = t1 * t3;
                double t5 = t1 * t4;
                double t6 = t1 * t5;
                double t7 = t1 * t6;

                xn = k7 * t7 + k6 * t6 + k5 * t5 + k4 * t4 + k3 * t3 + k2 * t2 + k1 * t1 + k0;
                vn = 7.0 * k7 * t6 + 6.0 * k6 * t5 + 5.0 * k5 * t4 + 4.0 * k4 * t3 + 3.0 * k3 * t2 + 2.0 * k2 * t1 + k1;
                an = 42.0 * k7 * t5 + 30.0 * k6 * t4 + 20.0 * k5 * t3 + 12.0 * k4 * t2 + 6.0 * k3 * t1 + 2.0 * k2;
                jn = 210.0 * k7 * t4 + 120.0 * k6 * t3 + 60.0 * k5 * t2 + 24 * k4 * t1 + 6.0 * k3;

                return xn;
            }

            public void Reset(double targetVelocity) {
                V = vn = targetVelocity;
                A = an = 0.0;
            }

        }

        private const double Ts = 0.004;

        private readonly Polynominal polyX = new Polynominal();

        private readonly Polynominal polyY = new Polynominal();

        private readonly Polynominal polyZ = new Polynominal();

        private readonly Polynominal polyA = new Polynominal();

        private readonly Polynominal polyB = new Polynominal();

        private readonly Polynominal polyC = new Polynominal();

        private readonly object syncLock = new object();

        private RobotVector targetPosition;

        private RobotVector targetVelocity;

        private RobotVector positionError;

        private double targetDuration;

        private double timeLeft;

        private bool targetPositionReached;

        public RobotVector TargetPosition {
            get {
                lock (syncLock) {
                    return targetPosition;
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

        public RobotVector VelocityP {
            get {
                lock (syncLock) {
                    return new RobotVector(polyX.Vp, polyY.Vp, polyZ.Vp, polyA.Vp, polyB.Vp, polyC.Vp);
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
        /// Current (theoretical) jerk
        /// </summary>
        public RobotVector Jerk {
            get {
                lock (syncLock) {
                    return new RobotVector(polyX.J, polyY.J, polyZ.J, polyA.J, polyB.J, polyC.J);
                }
            }
        }

        /// <summary>
        /// Current position error (diffrence between theoretical and actual position)
        /// </summary>
        public RobotVector PositionError {
            get {
                lock (syncLock) {
                    return positionError;
                }
            }
        }

        public TrajectoryGenerator7() {
        }

        public void Restart(RobotVector homePosition) {
            lock (syncLock) {
                targetPositionReached = true;
                targetPosition = homePosition;
                targetVelocity = RobotVector.Zero;
                positionError = RobotVector.Zero;
                targetDuration = 0.0;
                timeLeft = 0.0;
            }
        }

        public void SetTargetPosition(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            if (targetDuration <= 0.0) {
                throw new ArgumentException($"Duration value must be greater than 0, get {targetDuration}");
            }

            bool targetPositionChanged = !targetPosition.Compare(this.targetPosition, 0.1, 0.1);
            bool targetVelocityChanged = !targetVelocity.Compare(this.targetVelocity, 0.1, 0.1);
            bool targetDurationChanged = targetDuration != this.targetDuration;

            if (targetDurationChanged || targetPositionChanged || targetVelocityChanged) {
                lock (syncLock) {
                    targetPositionReached = false;
                    this.targetPosition = targetPosition;
                    this.targetVelocity = targetVelocity;
                    this.targetDuration = targetDuration;
                    timeLeft = targetDuration;
                }
            }
        }

        public RobotVector GetNextCorrection(RobotVector currentPosition) {
            lock (syncLock) {
                positionError = VelocityP;
                if (!targetPositionReached && timeLeft >= Ts && !(currentPosition.Compare(targetPosition, 0.1, 0.004))) {
                    double nx = polyX.GetNextValue(currentPosition.X, targetPosition.X, targetVelocity.X, timeLeft, Ts);
                    double ny = polyY.GetNextValue(currentPosition.Y, targetPosition.Y, targetVelocity.Y, timeLeft, Ts);
                    double nz = polyZ.GetNextValue(currentPosition.Z, targetPosition.Z, targetVelocity.Z, timeLeft, Ts);
                    double na = polyA.GetNextValue(currentPosition.A, targetPosition.A, targetVelocity.A, timeLeft, Ts);
                    double nb = polyB.GetNextValue(currentPosition.B, targetPosition.B, targetVelocity.B, timeLeft, Ts);
                    double nc = polyC.GetNextValue(currentPosition.C, targetPosition.C, targetVelocity.C, timeLeft, Ts);

                    RobotVector nextPosition = new RobotVector(nx, ny, nz, na, nb, nc);
                    timeLeft -= Ts;

                    return nextPosition - currentPosition;
                } else {
                    targetPositionReached = true;

                    polyX.Reset(targetVelocity.X);
                    polyY.Reset(targetVelocity.Y);
                    polyZ.Reset(targetVelocity.Z);
                    polyA.Reset(targetVelocity.A);
                    polyB.Reset(targetVelocity.B);
                    polyC.Reset(targetVelocity.C);

                    return RobotVector.Zero;
                }
            }
        }

    }
}