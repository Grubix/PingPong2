using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Windows;
using System.Windows.Controls;

namespace PingPong {
    public partial class OptiTrackPanel : UserControl {

        private Transformation robot1Transformation, robot2Transformation;

        public OptiTrackSystem OptiTrack { get; } = new OptiTrackSystem();

        public KUKARobot Robot1 { get; set; }

        public KUKARobot Robot2 { get; set; }

        public OptiTrackPanel() {
            InitializeComponent();

            positionChart.YAxisTitle = "Position (optiTrack base)";
            positionChart.AddSeries("Position X [mm]", "X", true);
            positionChart.AddSeries("Position Y [mm]", "Y", true);
            positionChart.AddSeries("Position Z [mm]", "Z", true);

            robot1PositionChart.YAxisTitle = "Position (robot1 base)";
            robot1PositionChart.AddSeries("Position X [mm]", "X", true);
            robot1PositionChart.AddSeries("Position Y [mm]", "Y", true);
            robot1PositionChart.AddSeries("Position Z [mm]", "Z", true);

            robot2PositionChart.YAxisTitle = "Position (robot2 base)";
            robot2PositionChart.AddSeries("Position X [mm]", "X", true);
            robot2PositionChart.AddSeries("Position Y [mm]", "Y", true);
            robot2PositionChart.AddSeries("Position Z [mm]", "Z", true);

            connectBtn.Click += Connect;
        }

        private void Connect(object sender, RoutedEventArgs e) {
            OptiTrack.FrameReceived += UpdateOptiTrackBasePositionChart;

            if (Robot1 != null && Robot1.OptiTrackTransformation != null) {
                robot1Transformation = Robot1.OptiTrackTransformation;
                OptiTrack.FrameReceived += UpdateRobot1BasePositionChart;
            }

            if (Robot2 != null && Robot2.OptiTrackTransformation != null) {
                robot2Transformation = Robot2.OptiTrackTransformation;
                OptiTrack.FrameReceived += UpdateRobot2BasePositionChart;
            }

            try {
                OptiTrack.Initialize();
                connectBtn.IsEnabled = false;
                disconnectBtn.IsEnabled = true;
            } catch (InvalidOperationException ex) {
                MessageBox.Show($"OptiTrack system initialization failed. Original error: \"{ex.Message}\"",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e) {
            connectBtn.IsEnabled = true;
            disconnectBtn.IsEnabled = false;

            OptiTrack.FrameReceived -= UpdateOptiTrackBasePositionChart;
            OptiTrack.FrameReceived -= UpdateRobot1BasePositionChart;
            OptiTrack.FrameReceived -= UpdateRobot2BasePositionChart;
        }

        private void UpdateOptiTrackBasePositionChart(OptiTrack.InputFrame frame) {
            positionChart.Update(new double[] {
                frame.BallPosition[0], frame.BallPosition[0], frame.BallPosition[0]
            });
        }

        private void UpdateRobot1BasePositionChart(OptiTrack.InputFrame frame) {
            var robot1BasePosition = robot1Transformation.Convert(frame.BallPosition);

            robot1PositionChart.Update(new double[] {
                robot1BasePosition[0], robot1BasePosition[0], robot1BasePosition[0]
            });
        }

        private void UpdateRobot2BasePositionChart(OptiTrack.InputFrame frame) {
            var robot2BasePosition = robot2Transformation.Convert(frame.BallPosition);

            robot1PositionChart.Update(new double[] {
                robot2BasePosition[0], robot2BasePosition[0], robot2BasePosition[0]
            });
        }

    }
}
