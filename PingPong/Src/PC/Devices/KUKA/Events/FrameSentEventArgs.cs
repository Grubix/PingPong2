using System;

namespace PingPong.KUKA {
    public class FrameSentEventArgs : EventArgs {

        public OutputFrame FrameSent { get; set; }

        public RobotVector ActualPosition { get; set; }

        public RobotVector TargetPosition { get; set; }

        public RobotVector GenPosition { get; set; }

        public RobotVector GenVelocity { get; set; }

        public RobotVector GenAcceleration { get; set; }

    }
}
