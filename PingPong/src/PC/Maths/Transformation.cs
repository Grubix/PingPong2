using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;

namespace PingPong.Maths {
    /// <summary>
    /// Represents 4x4 transformation matrix between two coordinate systems (A to B)
    /// </summary>
    public class Transformation {

        private Matrix<double> matrix;

        private Matrix<double> rotation;

        private Vector<double> translation;

        /// <summary>
        /// Gets or sets the value of transformation matrix at the given row and column
        /// </summary>
        /// <param name="i">row</param>
        /// <param name="j">column</param>
        /// <returns></returns>
        public double this[int i, int j] {
            get {
                return matrix[i, j];
            }
            set {
                matrix[i, j] = value;

                if (i < 3) {
                    if (j < 3) {
                        rotation[i, j] = value;
                    } else {
                        translation[i] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Calculate transformation between two coordinate systems (A to B),
        /// basing on <see href="https://en.wikipedia.org/wiki/Kabsch_algorithm">Kabsh algorithm</see>
        /// </summary>
        /// <param name="pointsA">Set of points in A coordinate system</param>
        /// <param name="pointsB">Set of points in B coordinate system</param>
        public Transformation(List<Vector<double>> pointsA, List<Vector<double>> pointsB) {
            if (pointsA.Count != pointsB.Count) {
                throw new ArgumentException("Coś tam po ang. ze liczba punktow musi sie zgadzac");
            }

            int pointsCount = pointsA.Count;
            var centroidA = Vector<double>.Build.Dense(3);
            var centroidB = Vector<double>.Build.Dense(3);

            foreach (var point in pointsA) {
                centroidA += point;
            }

            foreach (var point in pointsB) {
                centroidB += point;
            }

            centroidA /= pointsCount;
            centroidB /= pointsCount;

            // Covariance matrix
            var matrixH = Matrix<double>.Build.Dense(3, 3);

            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    double value = 0;

                    for (int k = 0; k < pointsCount; k++) {
                        value += (pointsA[k] - centroidA)[i] * (pointsB[k] - centroidB)[j];
                    }

                    matrixH[i, j] = value;
                }
            }

            var SVD = matrixH.Svd();
            var V = SVD.VT.Transpose();
            var UT = SVD.U.Transpose();

            if ((V * UT).Determinant() <= 0) { //TODO: niedokonca jasne czy ma byc < 0 czy <= 0,
                V[0, 2] *= -1;
                V[1, 2] *= -1;
                V[2, 2] *= -1;
            }

            rotation = V * UT;
            translation = -1 * rotation * centroidA + centroidB;

            matrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                { rotation[0, 0], rotation[0, 1], rotation[0, 2], translation[0] },
                { rotation[1, 0], rotation[1, 1], rotation[1, 2], translation[1] },
                { rotation[2, 0], rotation[2, 1], rotation[2, 2], translation[2] },
                { 0.0, 0.0, 0.0, 1.0 }
            });
        }

        public Transformation(Matrix<double> rotation, Vector<double> translation) {
            this.rotation = rotation.Clone();
            this.translation = translation.Clone();

            matrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                { rotation[0, 0], rotation[0, 1], rotation[0, 2], translation[0] },
                { rotation[1, 0], rotation[1, 1], rotation[1, 2], translation[1] },
                { rotation[2, 0], rotation[2, 1], rotation[2, 2], translation[2] },
                { 0.0, 0.0, 0.0, 1.0 }
            });
        }

        public Transformation() {
            matrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                { 1.0, 0.0, 0.0, 0.0 },
                { 0.0, 1.0, 0.0, 0.0 },
                { 0.0, 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 }
            });

            rotation = Matrix<double>.Build.DenseOfArray(new double[,] {
                { 1.0, 0.0, 0.0 },
                { 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 1.0 },
            });

            translation = Vector<double>.Build.DenseOfArray(new double[] {
                0.0, 0.0, 0.0
            });
        }

        /// <summary>
        /// Converts vector in A coordinate system to point B coordinate system
        /// </summary>
        /// <param name="pointInA">vector in A coordinate system</param>
        /// <returns></returns>
        public Vector<double> Convert(Vector<double> pointInA) {
            return rotation * pointInA + translation;
        }

    }
}