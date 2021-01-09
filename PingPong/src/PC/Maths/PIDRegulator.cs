namespace PingPong.Maths {
    /// <summary>
    /// https://www.scilab.org/discrete-time-pid-controller-implementation
    /// </summary>
    class PIDRegulator {

        private readonly double ku1, ku2, ke0, ke1, ke2;

        private double u0, u1, u2; // u[k], u[k-1], u[k-2]

        private double e1, e2; // e[k-1], e[k-2]

        public double Kp { get; }

        public double Ki { get; }

        public double Kd { get; }

        public double Ts { get; }

        public double N { get; }

        public PIDRegulator(double kp, double ki, double kd, double Ts, double N = 0) {
            Kp = kp;
            Ki = ki;
            Kd = kd;
            this.Ts = Ts;
            this.N = N;

            double a0, a1, a2, b0, b1, b2;

            if (N != 0) {
                a0 = 4.0 + 2 * N * Ts;
                a1 = -8.0;
                a2 = 4.0 - 2 * N * Ts;
                b0 = (4.0 + 2.0 * N * Ts) * Kp + (N * Ts * Ts + 2.0 * Ts) * Ki + 4.0 * N * Kd;
                b1 = 2.0 * N * Ts * Ts * Ki - 8.0 * (Kp + N * Kd);
                b2 = (4.0 - 2.0 * N * Ts) * Kp + (N * Ts * Ts - 2.0 * Ts) * Ki + 4.0 * N * Kd;
            } else {
                a0 = 2.0 * Ts;
                a1 = 0.0;
                a2 = -2.0 * Ts;
                b0 = Ts * Ts * Ki + 4.0 * Kd + 2.0 * Ts * Kp;
                b1 = 2.0 * Ts * Ts * Ki - 8.0 * Kd;
                b2 = Ts * Ts * Ki + 4.0 * Kd - 2.0 * Ts * Kp;
            }

            ku1 = a1 / a0;
            ku2 = a2 / a0;
            ke0 = b0 / a0;
            ke1 = b1 / a0;
            ke2 = b2 / a0;
        }

        public (double Output, double Error) Compute(double setpoint, double feedback) {
            double e0 = setpoint - feedback;

            e2 = e1;
            e1 = e0;
            u2 = u1;
            u1 = u0;

            u0 = ke0 * e0 + ke1 * e1 + ke2 * e2 - ku1 * u1 - ku2 * u2;

            //TODO: limity wyjscia, anti windup, cos?

            return (u0, e0);
        }

    }
}
