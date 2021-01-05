namespace PingPong.KUKA {
    public class RobotAxisPosition {

        public double A1 { get; }

        public double A2 { get; }

        public double A3 { get; }

        public double A4 { get; }

        public double A5 { get; }

        public double A6 { get; }

        public RobotAxisPosition(double A1, double A2, double A3, double A4, double A5, double A6) {
            this.A1 = A1;
            this.A2 = A2;
            this.A3 = A3;
            this.A4 = A4;
            this.A5 = A5;
            this.A6 = A6;
        }

        public override string ToString() {
            return $"[A1={A1:F3}, A2={A2:F3}, A3={A3:F3}, A4={A4:F3}, A5={A5:F3}, A6={A6:F3}]";
        }

    }
}
