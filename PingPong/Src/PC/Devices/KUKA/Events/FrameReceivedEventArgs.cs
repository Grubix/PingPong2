using System;

namespace PingPong.KUKA {
    public class FrameReceivedEventArgs : EventArgs {

        public InputFrame ReceivedFrame { get; set; }

        public RobotVector Position {
            get {
                return ReceivedFrame.Position;
            }
        }

        public RobotAxisVector AxisPosition {
            get {
                return ReceivedFrame.AxisPosition;
            }
        }

        public long IPOC {
            get {
                return ReceivedFrame.IPOC;
            }
        }

    }
}
