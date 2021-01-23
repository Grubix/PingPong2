using System;

namespace PingPong.KUKA {
    public class FrameReceivedEventArgs : EventArgs {

        public InputFrame ReceivedFrame { get; set; }

        public RobotVector ActualPosition { get; set; }

        public RobotVector TargetPosition { get; set; }

        public RobotVector GenPosition { get; set; }

        public RobotVector GenVelocity { get; set; }

        public RobotVector GenAcceleration { get; set; }

        public bool IsTargetPositionReached { get; set; }

    }
}
