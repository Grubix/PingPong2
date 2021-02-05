using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using System;

namespace PingPong.Applications {
    public class PingDataReadyEventArgs : EventArgs {

        public Vector<double> PredictedBallPosition { get; set; }

        public Vector<double> PredictedBallVelocity { get; set; }

        public Vector<double> ActualBallPosition { get; set; }

        public RobotVector ActualRobotPosition { get; set; }

        public double PredictedTimeToHit { get; set; }

        public double LastBounceHeight { get; set; }

        public double TargetBounceHeight { get; set; }

        public int BounceCounter { get; set; }

    }
}
