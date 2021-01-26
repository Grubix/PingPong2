namespace PingPong.KUKA {
    public class RobotMovement {

        public RobotVector TargetPosition { get; }

        public RobotVector TargetVelocity { get; }

        public double TargetDuration { get; }

        public RobotMovement(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            TargetPosition = targetPosition;
            TargetVelocity = targetVelocity;
            TargetDuration = targetDuration;
        }

    }
}
