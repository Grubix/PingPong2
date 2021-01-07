using MathNet.Numerics.LinearAlgebra;
using NatNetML;

namespace PingPong.OptiTrack {
    /// <summary>
    /// https://v22.wiki.optitrack.com/index.php?title=NatNet:_Data_Types
    /// </summary>
    public class InputFrame {

        public Vector<double> BallPosition { get; }

        public double DeltaTime { get; }

        public InputFrame(FrameOfMocapData data, double frameDeltaTime) {
            BallPosition = Vector<double>.Build.DenseOfArray(new double[] {
                data.OtherMarkers[0].x * 1000.0,
                data.OtherMarkers[0].y * 1000.0,
                data.OtherMarkers[0].z * 1000.0
            });

            DeltaTime = frameDeltaTime;
        }

    }
}
