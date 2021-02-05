using MathNet.Numerics.LinearAlgebra;

namespace PingPong.Maths {
    class KalmanFilter {

        private readonly KalmanModel model;

        private readonly Matrix<double> I;

        private Matrix<double> P, K;

        private Vector<double> U;

        public Vector<double> PredictedState { get; private set; }

        public Vector<double> CorrectedState { get; private set; }

        public KalmanFilter(KalmanModel model) {
            //TODO: sprawdzenie wymiarow macierzy modelu
            //Matrix<double> F = model.F;
            //Matrix<double> B = model.B;
            //Matrix<double> H = model.H;
            //Matrix<double> Q = model.Q;
            //Matrix<double> R = model.R;

            //if (F.RowCount != F.ColumnCount) {
            //    //TODO: Err A -> kwadrat
            //}

            //if (B.RowCount != F.RowCount) {
            //    //TODO: ERR B -> A.Rows x B.Cols
            //}

            //if (H.ColumnCount != F.ColumnCount) {
            //    //TODO: ERR H -> M x N
            //}

            //if (Q.RowCount != F.RowCount || Q.ColumnCount != F.RowCount) {
            //    //TODO: Err P -> N x N
            //    //TODO: Err Q -> A.Rows x A.Rows
            //}

            I = Matrix<double>.Build.DenseIdentity(model.StateDim, model.StateDim);
            this.model = model;
            Reset();
        }

        public Vector<double> Compute(Vector<double> measurement, Vector<double> input = null) {
            Matrix<double> F = model.F;
            Matrix<double> B = model.B;
            Matrix<double> H = model.H;
            Matrix<double> Q = model.Q;
            Matrix<double> R = model.R;

            if (input != null) {
                U = input;
            }

            // Predict state
            PredictedState = F * CorrectedState + B * U;
            P = F * P * F.Transpose() + Q;

            // Update state
            K = P * H.Transpose() * (H * P * H.Transpose() + R).Inverse();
            CorrectedState = PredictedState + K * (measurement - H * PredictedState);
            P = (I - K * H) * P;

            return CorrectedState;
        }

        public Vector<double> Compute(double measurement) {
            var vector = Vector<double>.Build.Dense(1);
            vector[0] = measurement;

            return Compute(vector);
        }

        public void Reset() {
            P = Matrix<double>.Build.DenseIdentity(model.StateDim, model.StateDim);
            K = Matrix<double>.Build.DenseIdentity(model.StateDim, model.OutputDim);

            U = Vector<double>.Build.Dense(model.InputDim);
            PredictedState = Vector<double>.Build.Dense(model.StateDim);
            CorrectedState = Vector<double>.Build.Dense(model.StateDim);
        }

    }
}