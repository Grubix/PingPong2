using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;

namespace PingPong.Maths {
    /// <summary>
    /// https://mathworld.wolfram.com/LeastSquaresFittingPolynomial.html
    /// </summary>
    class Polyfit {

        /// <summary>
        /// Polynominal order
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// List of paired x,y values
        /// </summary>
        public List<(double X, double Y)> Values { get; }

        /// <summary>
        /// Coefficients calculated with CalculateCoefficients() method
        /// </summary>
        public List<double> Coefficients;

        public Polyfit(int order) {
            Order = order;
            Values = new List<(double X, double Y)>();
        }

        public void AddPoint(double x, double y) {
            Values.Add((x, y));
        }

        public List<double> CalculateCoefficients() {
            if (Values.Count == 0) {
                throw new InvalidOperationException("At least one XY point is required to calculate polynominal coefficients");
            }

            var coefficients = new List<double>();

            // X - Vandermonde matrix; Y - y values vector
            var X = Matrix<double>.Build.Dense(Values.Count, Order + 1);
            var Y = Matrix<double>.Build.Dense(Values.Count, 1);

            for (int i = 0; i < Values.Count; i++) {
                X[i, 0] = 1.0;
                Y[i, 0] = Values[i].Y;

                for (int j = 1; j < X.ColumnCount; j++) {
                    X[i, j] = X[i, j-1] * Values[i].X;
                }
            }

            var XT = X.Transpose();
            var XTX = XT * X;

            // Check if XTX matrix is inversible
            if (XTX.Determinant() == 0.0) {
                for (int i = 0; i < Order + 1; i++) {
                    coefficients.Add(0.0);
                }

                Coefficients = new List<double>(coefficients);
                return coefficients;
            }

            // C - polynominal coefficients vector
            var C = XTX.Inverse() * XT * Y;

            for (int i = 0; i < C.RowCount; i++) {
                coefficients.Add(C[i, 0]);
            }

            Coefficients = new List<double>(coefficients);
            return coefficients;
        }

    }
}