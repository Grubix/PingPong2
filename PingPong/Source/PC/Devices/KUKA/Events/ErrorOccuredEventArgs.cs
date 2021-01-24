using System;

namespace PingPong.KUKA {
    public class ErrorOccuredEventArgs : EventArgs {

        public string RobotIp { get; set; }

        public Exception Exception { get; set; }

    }
}
