using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using System;

namespace PingPong.Applications {
    class PingDataReadyEventArgs : EventArgs {

        public Vector<double> PredictedBallPosition { get; set; }

        public Vector<double> PredictedBallVelocity { get; set; }

        public Vector<double> ActualBallPosition { get; set; }

        public RobotVector ActualRobotPosition { get; set; }

        public double PredictedTimeToHit { get; set; }

        public double BallSetpointX { get; set; }

        public double BallSetpointY { get; set; }

        public double BallSetpointZ { get; set; }

    }
}
