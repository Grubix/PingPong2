using System;

namespace PingPong.KUKA {
    public class RobotLimits {

        public (double X, double Y, double Z) LowerWorkspacePoint { get; }

        public (double X, double Y, double Z) UpperWorkspacePoint { get; }

        public (double Min, double Max) A1AxisLimit { get; }

        public (double Min, double Max) A2AxisLimit { get; }

        public (double Min, double Max) A3AxisLimit { get; }

        public (double Min, double Max) A4AxisLimit { get; }

        public (double Min, double Max) A5AxisLimit { get; }

        public (double Min, double Max) A6AxisLimit { get; }

        public (double XYZ, double ABC) CorrectionLimit { get; }

        public (double XYZ, double ABC) VelocityLimit { get; }

        public (double XYZ, double ABC) AccelerationLimit { get; }

        public RobotLimits(
            (double X, double Y, double Z) lowerWorkspaceLimit, 
            (double X, double Y, double Z) upperWorkspaceLimit, 
            (double Min, double Max) a1AxisLimit, 
            (double Min, double Max) a2AxisLimit, 
            (double Min, double Max) a3AxisLimit, 
            (double Min, double Max) a4AxisLimit, 
            (double Min, double Max) a5AxisLimit, 
            (double Min, double Max) a6AxisLimit, 
            (double XYZ, double ABC) correctionLimit,
            (double XYZ, double ABC) velocityLimit,
            (double XYZ, double ABC) accelerationLimit
        ) {
            LowerWorkspacePoint = lowerWorkspaceLimit;
            UpperWorkspacePoint = upperWorkspaceLimit;
            A1AxisLimit = a1AxisLimit;
            A2AxisLimit = a2AxisLimit;
            A3AxisLimit = a3AxisLimit;
            A4AxisLimit = a4AxisLimit;
            A5AxisLimit = a5AxisLimit;
            A6AxisLimit = a6AxisLimit;
            CorrectionLimit = correctionLimit;
            VelocityLimit = velocityLimit;
            AccelerationLimit = accelerationLimit;
        }

        public bool CheckPosition(RobotVector position) {
            bool checkX = position.X >= LowerWorkspacePoint.X && position.X <= UpperWorkspacePoint.X;
            bool checkY = position.Y >= LowerWorkspacePoint.Y && position.Y <= UpperWorkspacePoint.Y;
            bool checkZ = position.Z >= LowerWorkspacePoint.Z && position.Z <= UpperWorkspacePoint.Z;

            return checkX && checkY && checkZ;
        }

        public bool CheckAxisPosition(RobotAxisVector axisPosition) {
            bool checkA1 = axisPosition.A1 >= A1AxisLimit.Min && axisPosition.A1 <= A1AxisLimit.Max;
            bool checkA2 = axisPosition.A2 >= A2AxisLimit.Min && axisPosition.A2 <= A2AxisLimit.Max;
            bool checkA3 = axisPosition.A3 >= A3AxisLimit.Min && axisPosition.A3 <= A3AxisLimit.Max;
            bool checkA4 = axisPosition.A4 >= A4AxisLimit.Min && axisPosition.A4 <= A4AxisLimit.Max;
            bool checkA5 = axisPosition.A5 >= A5AxisLimit.Min && axisPosition.A5 <= A5AxisLimit.Max;
            bool checkA6 = axisPosition.A6 >= A6AxisLimit.Min && axisPosition.A6 <= A6AxisLimit.Max;

            return checkA1 && checkA2 && checkA3 && checkA4 && checkA5 && checkA6;
        }

        public bool CheckCorrection(RobotVector correction) {
            bool checkX = Math.Abs(correction.X) <= CorrectionLimit.XYZ;
            bool checkY = Math.Abs(correction.Y) <= CorrectionLimit.XYZ;
            bool checkZ = Math.Abs(correction.Z) <= CorrectionLimit.XYZ;
            bool checkA = Math.Abs(correction.A) <= CorrectionLimit.ABC;
            bool checkB = Math.Abs(correction.B) <= CorrectionLimit.ABC;
            bool checkC = Math.Abs(correction.C) <= CorrectionLimit.ABC;

            return checkX && checkY && checkZ && checkA && checkB && checkC;
        }

        public bool CheckVelocity(RobotVector velocity) {
            bool checkX = Math.Abs(velocity.X) <= VelocityLimit.XYZ;
            bool checkY = Math.Abs(velocity.Y) <= VelocityLimit.XYZ;
            bool checkZ = Math.Abs(velocity.Z) <= VelocityLimit.XYZ;
            bool checkA = Math.Abs(velocity.A) <= VelocityLimit.ABC;
            bool checkB = Math.Abs(velocity.B) <= VelocityLimit.ABC;
            bool checkC = Math.Abs(velocity.C) <= VelocityLimit.ABC;

            return checkX && checkY && checkZ && checkA && checkB && checkC;
        }

        public bool CheckAcceleration(RobotVector acceleration) {
            bool checkX = Math.Abs(acceleration.X) <= AccelerationLimit.XYZ;
            bool checkY = Math.Abs(acceleration.Y) <= AccelerationLimit.XYZ;
            bool checkZ = Math.Abs(acceleration.Z) <= AccelerationLimit.XYZ;
            bool checkA = Math.Abs(acceleration.A) <= AccelerationLimit.ABC;
            bool checkB = Math.Abs(acceleration.B) <= AccelerationLimit.ABC;
            bool checkC = Math.Abs(acceleration.C) <= AccelerationLimit.ABC;

            return checkX && checkY && checkZ && checkA && checkB && checkC;
        }

        public bool CheckMove(RobotVector targetPosition, RobotVector targetVelocity, double targetDuration) {
            return CheckPosition(targetPosition) && CheckVelocity(targetVelocity) && targetDuration > 0.032;
        }

    }
}
