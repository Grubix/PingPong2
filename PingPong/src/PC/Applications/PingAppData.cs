using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;

namespace PingPong.Applications {
    class PingAppData {

        public double PredictedTimeOfFlight { get; set; }

        public Vector<double> PredictedBallPosition { get; set; }

        public Vector<double> ActualBallPosition { get; set; }

        public RobotVector ActualRobotPosition { get; set; }

        //TODO: mozna dorobic wiecej w zaleznosci od potrzeb

    }
}
