namespace PingPong.Maths {
    /// <summary>
    /// https://www.scilab.org/discrete-time-pid-controller-implementation
    /// </summary>
    class PIRegulator {

        private double ku1, ke0, ke1;

        private double u0, u1;

        private double e0, e1;

        private double Ts;

        public double Kp { get; }

        public double Ki { get; }

        public double setPoint { get; }

        public PIRegulator(double kp, double ki, double setPoint, double Ts = 0.004) {
            Kp = kp;
            Ki = ki;
            this.Ts = Ts;
            this.setPoint = setPoint;
            u1 = e1 = 0.0;

            CalculateCoeffs();
        }

        private void CalculateCoeffs() {
            double a0, a1, b0, b1;

            a0 = 1.0;
            a1 = -1.0;
            b0 = Kp + Ki * Ts;
            b1 = -Kp;

            ku1 = a1 / a0;
            ke0 = b0 / a0;
            ke1 = b1 / a0;
        }

        public double ComputeU(double feedback, double Ts = -1) {
            if (Ts != -1) {
                this.Ts = Ts;
                CalculateCoeffs();
            }
            e0 = setPoint - feedback;
            u0 = ke0 * e0 + ke1 * e1 - ku1 * u1;
            u0 = Kp * e0;

            return u0;
        }

        public void Shift() {
            e1 = e0;
            u1 = u0;
        }
    }
}
