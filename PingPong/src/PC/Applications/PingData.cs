using MathNet.Numerics.LinearAlgebra;

namespace PingPong.Applications {
    class PingData {

        public double PredictedTimeOfFlight { get; set; }

        public Vector<double> PredictedBallPosition { get; set; }

        public Vector<double> ActualBallPosition { get; set; }

        //TODO: mozna dorobic wiecej w zaleznosci od potrzeb

    }
}
