using MathNet.Numerics.LinearAlgebra;
using System;

namespace PingPong.KUKA {
    /// <summary>
    /// Represents KUKA robot vector
    /// </summary>
    public class RobotVector : ICloneable {

        public static readonly RobotVector Zero = new RobotVector();

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double A { get; }

        public double B { get; }

        public double C { get; }

        public Vector<double> XYZ {
            get {
                return Vector<double>.Build.DenseOfArray(new double[] { X, Y, Z });
            }
        }

        public Vector<double> ABC {
            get {
                return Vector<double>.Build.DenseOfArray(new double[] { A, B, C });
            }
        }

        public RobotVector(double X, double Y, double Z, double A, double B, double C) {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.A = A;
            this.B = B;
            this.C = C;
        }

        public RobotVector() : 
            this(0.0, 0.0, 0.0, 0.0, 0.0, 0.0) {
        }

        public RobotVector(double X, double Y, double Z) : 
            this(X, Y, Z, 0.0, 0.0, 0.0) {
        }

        public RobotVector(Vector<double> XYZ, Vector<double> ABC) : 
            this(XYZ[0], XYZ[1], XYZ[2], ABC[0], ABC[1], ABC[2]) {
        }

        public RobotVector(double X, double Y, double Z, Vector<double> ABC) : 
            this(X, Y, Z, ABC[0], ABC[1], ABC[2]) {
        }

        public RobotVector(Vector<double> XYZ, double A, double B, double C) : 
            this(XYZ[0], XYZ[1], XYZ[2], A, B, C) {
        }

        public bool Compare(RobotVector vectorToCompare, double xyzTolerance, double abcTolerance) {
            return
                Math.Abs(X - vectorToCompare.X) <= xyzTolerance &&
                Math.Abs(Y - vectorToCompare.Y) <= xyzTolerance &&
                Math.Abs(Z - vectorToCompare.Z) <= xyzTolerance &&
                Math.Abs(A - vectorToCompare.A) <= abcTolerance &&
                Math.Abs(B - vectorToCompare.B) <= abcTolerance &&
                Math.Abs(C - vectorToCompare.C) <= abcTolerance;
        }

        public double[] ToArray() {
            return new double[] {
                X, Y, Z, A, B, C
            };
        }

        public override string ToString() {
            return $"[X={X:F3}, Y={Y:F3}, Z={Z:F3}, A={A:F3}, B={B:F3}, C={C:F3}]";
        }

        public object Clone() {
            return new RobotVector(X, Y, Z, A, B, C);
        }

        public static RobotVector operator +(RobotVector vec1, RobotVector vec2) {
            return new RobotVector(
                vec1.X + vec2.X,
                vec1.Y + vec2.Y,
                vec1.Z + vec2.Z,
                vec1.A + vec2.A,
                vec1.B + vec2.B,
                vec1.C + vec2.C
            );
        }

        public static RobotVector operator -(RobotVector vec1, RobotVector vec2) {
            return new RobotVector(
                vec1.X - vec2.X,
                vec1.Y - vec2.Y,
                vec1.Z - vec2.Z,
                vec1.A - vec2.A,
                vec1.B - vec2.B,
                vec1.C - vec2.C
            );
        }

    }
}