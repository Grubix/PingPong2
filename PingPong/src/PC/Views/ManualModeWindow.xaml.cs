using PingPong.KUKA;
using System;
using System.Windows;

namespace PingPong {
    public partial class ManualModeWindow : Window {

        private readonly KUKARobot robot;

        public ManualModeWindow(KUKARobot robot) {
            InitializeComponent();

            this.robot = robot;
            robotIpAdress.Content += robot.ToString();

            moveToBtn.Click += MoveTo;
            moveToResetBtn.Click += ResetMoveToFields;

            shiftBtn.Click += Shift;
            shiftResetBtn.Click += ResetShiftFields;
        }

        private void MoveTo(object sender, RoutedEventArgs e) {
            try {
                double x = double.Parse(moveToX.Text);
                double y = double.Parse(moveToY.Text);
                double z = double.Parse(moveToZ.Text);
                double a = double.Parse(moveToA.Text);
                double b = double.Parse(moveToB.Text);
                double c = double.Parse(moveToC.Text);

                double movementTime = Math.Max(double.Parse(moveToTime.Text), 2);
                moveToTime.Text = movementTime.ToString();

                RobotVector targetPosition = new RobotVector(x, y, z, a, b, c);
                robot.MoveTo(targetPosition, RobotVector.Zero, movementTime);
            } catch(Exception ex) {
                MainWindow.ShowErrorDialog("Unable to move robot to specified target position.", ex);
            }
        }

        private void Shift(object sender, RoutedEventArgs e) {
            try {
                double x = double.Parse(shiftX.Text);
                double y = double.Parse(shiftY.Text);
                double z = double.Parse(shiftZ.Text);
                double a = double.Parse(shiftA.Text);
                double b = double.Parse(shiftB.Text);
                double c = double.Parse(shiftC.Text);

                double movementTime = Math.Max(double.Parse(shiftTime.Text), 2);
                shiftTime.Text = movementTime.ToString();

                RobotVector deltaPosition = new RobotVector(x, y, z, a, b, c);
                robot.Shift(deltaPosition, RobotVector.Zero, movementTime);

                ResetShiftFields(null, null);
            } catch (Exception ex) {
                MainWindow.ShowErrorDialog("Unable to shift robot by specified delta position.", ex);
            }
        }

        private void ResetMoveToFields(object sender, RoutedEventArgs e) {
            if (robot.IsInitialized()) {
                RobotVector actualPosition = robot.Position;

                moveToX.Text = actualPosition.X.ToString();
                moveToY.Text = actualPosition.Y.ToString();
                moveToZ.Text = actualPosition.Z.ToString();
                moveToA.Text = actualPosition.A.ToString();
                moveToB.Text = actualPosition.B.ToString();
                moveToC.Text = actualPosition.C.ToString();
            }
        }

        private void ResetShiftFields(object sender, RoutedEventArgs e) {
            shiftX.Text = "0.0";
            shiftY.Text = "0.0";
            shiftZ.Text = "0.0";
            shiftA.Text = "0.0";
            shiftB.Text = "0.0";
            shiftC.Text = "0.0";
        }
    }
}
