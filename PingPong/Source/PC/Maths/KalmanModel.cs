using MathNet.Numerics.LinearAlgebra;

namespace PingPong.Maths {
    public abstract class KalmanModel {

        /// <summary>
        /// State gain
        /// </summary>
        public Matrix<double> F { get; protected set; }

        /// <summary>
        /// Input gain
        /// </summary>
        public Matrix<double> B { get; protected set; }

        /// <summary>
        /// Output gain
        /// </summary>
        public Matrix<double> H { get; protected set; }

        /// <summary>
        /// Process noise covariance
        /// </summary>
        public Matrix<double> Q { get; protected set; }

        /// <summary>
        /// Measurement noise covariance
        /// </summary>
        public Matrix<double> R { get; protected set; }

        public int StateDim { 
            get {
                return F.ColumnCount;
            }
        }

        public int InputDim {
            get {
                return B.ColumnCount;
            }
        }

        public int OutputDim {
            get {
                return H.RowCount;
            }
        }

    }
}