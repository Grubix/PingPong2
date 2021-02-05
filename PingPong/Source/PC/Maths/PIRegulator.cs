namespace PingPong.Maths {
    /// <summary>
    /// https://www.scilab.org/discrete-time-pid-controller-implementation
    /// </summary>
    class PIRegulator {

        private double ku1, ke0, ke1;

        private double u0, u1;

        private double e0, e1;

        public double Ts { get; private set; } = 0.004;

        public double Kp { get; private set; }

        public double Ki { get; private set; }

        public double SetPoint { get; private set; }

        public PIRegulator() {

        }

        public PIRegulator(double kp, double ki, double setPoint) {
            SetParams(kp, ki, setPoint);
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

        public void SetParams(double kp, double ki, double setPoint) {
            Kp = kp;
            Ki = ki;
            SetPoint = setPoint;
            //u1 = e1 = 0.0; //TODO: TO TU TAK POWINNO BYC ?

            CalculateCoeffs();
        }

        public double ComputeU(double feedback, double ts = -1) {
            if (ts != -1) {
                Ts = ts;
                CalculateCoeffs();
            }
            e0 = SetPoint - feedback;
            u0 = ke0 * e0 + ke1 * e1 - ku1 * u1;
            //u0 = Kp * e0;

            return u0;
        }

        public void Shift() {
            e1 = e0;
            u1 = u0;
        }

    }
}
