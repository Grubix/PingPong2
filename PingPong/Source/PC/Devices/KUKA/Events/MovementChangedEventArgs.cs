using System;

namespace PingPong.KUKA {
    public class MovementChangedEventArgs : EventArgs {

        public RobotVector Position { get; set; }

        public RobotVector Velocity { get; set; }

        public RobotVector Acceleration { get; set; }

        public RobotMovement[] MovementsStack { get; set; }

    }
}
