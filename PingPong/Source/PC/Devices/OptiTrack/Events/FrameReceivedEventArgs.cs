using MathNet.Numerics.LinearAlgebra;
using System;

namespace PingPong.OptiTrack {
    public class FrameReceivedEventArgs : EventArgs {

        public InputFrame ReceivedFrame { get; set; }

        public Vector<double> PrevBallPosition { get; set; }

        public Vector<double> BallPosition {
            get {
                return ReceivedFrame.BallPosition;
            }
        }

        public double FrameDeltaTime {
            get {
                return ReceivedFrame.DeltaTime;
            }
        }

    }
}
