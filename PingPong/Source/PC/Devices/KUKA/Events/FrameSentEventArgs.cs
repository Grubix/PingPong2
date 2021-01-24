using System;

namespace PingPong.KUKA {
    public class FrameSentEventArgs : EventArgs {

        public OutputFrame FrameSent { get; set; }

        public RobotVector Position { get; set; }

        public RobotVector TargetPosition { get; set; }

        public RobotVector TargetVelocity { get; set; }

        public double TargetDuration { get; set; }

        public RobotVector Correction {
            get {
                return FrameSent.Correction;
            }
        }

        public long IPOC {
            get {
                return FrameSent.IPOC;
            }
        }

    }
}
