using MathNet.Numerics.LinearAlgebra;
using PingPong.Applications;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PingPong {
    public partial class OptiTrackPanel : UserControl {

        private bool isPlotFrozen;

        private Robot robot1;

        private Robot robot2;

        private Transformation robot1Transformation;

        private Transformation robot2Transformation;

        private LiveChart activeChart;

        public OptiTrackSystem OptiTrack { get; }

        public OptiTrackPanel() {
            InitializeComponent();
            InitializeCharts();

            OptiTrack = new OptiTrackSystem();
            OptiTrack.Initialized += () => {
                hostApp.Text = OptiTrack.ServerDescription.HostApp;
                hostName.Text = OptiTrack.ServerDescription.HostComputerName;
                hostAdress.Text = OptiTrack.ServerDescription.HostComputerAddress.ToString(); //TODO:
                natnetVersion.Text = OptiTrack.ServerDescription.NatNetVersion.ToString(); //TODO:
            };

            connectBtn.Click += Connect;
            disconnectBtn.Click += Disconnect;
            freezeBtn.Click += FreezeCharts;
            fitToDataBtn.Click += FitChartsToData;
            resetZoomBtn.Click += ResetChartsZoom;
            screenshotBtn.Click += TakeChartScreenshot;

            activeChart = positionChart;
            tabControl.SelectionChanged += (s, e) => {
                activeChart = (LiveChart)tabControl.SelectedContent;
            };

            // CTRL + S -> save active chart to png image
            Loaded += (s, e) => Focus();
            KeyDown += (s, e) => {
                if (e.Key == Key.S && Keyboard.IsKeyDown(Key.LeftCtrl)) {
                    TakeChartScreenshot(null, null);
                }
            };

            //positionChart.RefreshDelay = 10;

            //BallFlightEmulator bfs = new BallFlightEmulator(x0: 1000, y0: 700, z0: 250, vx0: -1500, vy0: 50, vz0: 2600);
            ////BallFlightEmulator bfs = new BallFlightEmulator(x0: 0, y0: 800, z0: 180, vx0: 150, vy0: -90, vz0: 1600);
            //HitPrediction prediction = new HitPrediction();
            //prediction.Reset(180);

            //RobotLimits limits = new RobotLimits(
            //    lowerWorkspaceLimit: (-250, 700, 150),
            //    upperWorkspaceLimit: (250, 950, 400),
            //    a1AxisLimit: (-360, 360),
            //    a2AxisLimit: (-360, 360),
            //    a3AxisLimit: (-360, 360),
            //    a4AxisLimit: (-360, 360),
            //    a5AxisLimit: (-360, 360),
            //    a6AxisLimit: (-360, 360),
            //    correctionLimit: (6, 0.1)
            //);
            //RobotConfig config2 = new RobotConfig(0, limits, null);
            //RobotEmulator emulator = new RobotEmulator(config2);
            //emulator.Initialize();

            //bfs.PositionChanged += (position, time) => {
            //    prediction.AddMeasurement(position, time);
            //    //Console.WriteLine(prediction.Position);

            //    if (prediction.IsReady && prediction.TimeToHit > 0.1) {
            //        var targetPos = new RobotVector(prediction.Position, emulator.HomePosition.ABC);
            //        Console.WriteLine(targetPos);
            //        if (emulator.Limits.CheckPosition(targetPos)) {
            //            emulator.MoveTo(targetPos, RobotVector.Zero, prediction.TimeToHit);
            //        }
            //    }

            //    if (positionChart.IsReady) {
            //        var pos = emulator.Position;

            //        positionChart.Update(new double[] {
            //            position[0], position[1], position[2], prediction.Position[0], prediction.Position[1], prediction.Position[2], pos.X, pos.Y, pos.Z
            //         });
            //    } else {
            //        positionChart.Tick();
            //    }
            //};

            //bfs.Start(5, 5, 5);
        }

        public void Initialize(Robot robot1, Robot robot2) {
            this.robot1 = robot1;
            this.robot2 = robot2;

            if (robot1.OptiTrackTransformation != null) {
                robot1Transformation = robot1.OptiTrackTransformation;
            }

            if (robot2.OptiTrackTransformation != null) {
                robot2Transformation = robot2.OptiTrackTransformation;
            }

            robot1.Initialized += () => robot1Transformation = robot1.OptiTrackTransformation;
            robot2.Initialized += () => robot2Transformation = robot2.OptiTrackTransformation;
        }

        private void UpdateOptiTrackBasePositionChart(OptiTrack.InputFrame frame) {
            if (isPlotFrozen) {
                return;
            }

            if (positionChart.IsReady) {
                positionChart.Update(new double[] {
                    frame.BallPosition[0], frame.BallPosition[1], frame.BallPosition[2]
                });
                Dispatcher.Invoke(() => {
                    actualPositionX.Text = frame.BallPosition[0].ToString("F3");
                    actualPositionY.Text = frame.BallPosition[1].ToString("F3");
                    actualPositionZ.Text = frame.BallPosition[2].ToString("F3");
                });
            } else {
                positionChart.Tick();
            }
        }

        private void UpdateRobot1BasePositionChart(OptiTrack.InputFrame frame) {
            if (isPlotFrozen) {
                return;
            }

            var robot1BasePosition = robot1Transformation.Convert(frame.BallPosition);

            if (robot1PositionChart.IsReady) {
                robot1PositionChart.Update(new double[] {
                    robot1BasePosition[0], robot1BasePosition[1], robot1BasePosition[2]
                });
                Dispatcher.Invoke(() => {
                    robot1BaseActualPositionX.Text = robot1BasePosition[0].ToString("F3");
                    robot1BaseActualPositionY.Text = robot1BasePosition[1].ToString("F3");
                    robot1BaseActualPositionZ.Text = robot1BasePosition[2].ToString("F3");
                });
            } else {
                robot1PositionChart.Tick();
            }
        }

        private void UpdateRobot2BasePositionChart(OptiTrack.InputFrame frame) {
            if (isPlotFrozen) {
                return;
            }

            var robot2BasePosition = robot2Transformation.Convert(frame.BallPosition);

            if (robot2PositionChart.IsReady) {
                robot2PositionChart.Update(new double[] {
                    robot2BasePosition[0], robot2BasePosition[1], robot2BasePosition[2]
                });
                Dispatcher.Invoke(() => {
                    robot2BaseActualPositionX.Text = robot2BasePosition[0].ToString("F3");
                    robot2BaseActualPositionY.Text = robot2BasePosition[1].ToString("F3");
                    robot2BaseActualPositionZ.Text = robot2BasePosition[2].ToString("F3");
                });
            } else {
                robot2PositionChart.Tick();
            }
        }

        private void InitializeCharts() {
            positionChart.YAxisTitle = "Position (optiTrack base)";
            positionChart.AddSeries("Ball position X [mm]", "X", true);
            positionChart.AddSeries("Ball position Y [mm]", "Y", true);
            positionChart.AddSeries("Ball position Z [mm]", "Z", true);
            //positionChart.AddSeries("Ball pred. position X [mm]", "Xp", false);
            //positionChart.AddSeries("Ball pred. position Y [mm]", "Yp", false);
            //positionChart.AddSeries("Ball pred. position Z [mm]", "Zp", false);
            //positionChart.AddSeries("Robot position X [mm]", "X_R", true);
            //positionChart.AddSeries("Robot position Y [mm]", "Y_R", true);
            //positionChart.AddSeries("Robot position Z [mm]", "Z_R", true);

            robot1PositionChart.YAxisTitle = "Position (robot1 base)";
            robot1PositionChart.AddSeries("Robot 1 base ball position X [mm]", "X", true);
            robot1PositionChart.AddSeries("Robot 1 base ball position Y [mm]", "Y", true);
            robot1PositionChart.AddSeries("Robot 1 base ball position Z [mm]", "Z", true);

            robot2PositionChart.YAxisTitle = "Position (robot2 base)";
            robot2PositionChart.AddSeries("Robot 2 base ball position X [mm]", "X", true);
            robot2PositionChart.AddSeries("Robot 2 base ball position Y [mm]", "Y", true);
            robot2PositionChart.AddSeries("Robot 2 base ball position Z [mm]", "Z", true);
        }

        private void Connect(object sender, RoutedEventArgs e) {
            OptiTrack.FrameReceived += UpdateOptiTrackBasePositionChart;

            if (robot1Transformation != null) {
                OptiTrack.FrameReceived += UpdateRobot1BasePositionChart;
            }

            if (robot2Transformation != null) {
                OptiTrack.FrameReceived += UpdateRobot2BasePositionChart;
            }

            try {
                OptiTrack.Initialize();
                connectBtn.IsEnabled = false;
                disconnectBtn.IsEnabled = true;
            } catch (InvalidOperationException ex) {
                MainWindow.ShowErrorDialog("OptiTrack system initialization failed.", ex);
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e) {
            OptiTrack.Uninitialize();

            OptiTrack.FrameReceived -= UpdateOptiTrackBasePositionChart;
            OptiTrack.FrameReceived -= UpdateRobot1BasePositionChart;
            OptiTrack.FrameReceived -= UpdateRobot2BasePositionChart;

            connectBtn.IsEnabled = true;
            disconnectBtn.IsEnabled = false;
        }

        private void FreezeCharts(object sender, RoutedEventArgs e) {
            if (isPlotFrozen) {
                positionChart.Clear();
                robot1PositionChart.Clear();
                robot2PositionChart.Clear();

                positionChart.BlockZoomAndPan();
                robot1PositionChart.BlockZoomAndPan();
                robot2PositionChart.BlockZoomAndPan();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
                fitToDataBtn.IsEnabled = false;
                screenshotBtn.IsEnabled = false;
            } else {
                positionChart.UnblockZoomAndPan();
                robot1PositionChart.UnblockZoomAndPan();
                robot2PositionChart.UnblockZoomAndPan();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
                screenshotBtn.IsEnabled = true;

                FitChartsToData(null, null);
            }
        }

        private void FitChartsToData(object sender, RoutedEventArgs e) {
            positionChart.FitToData();
            robot1PositionChart.FitToData();
            robot2PositionChart.FitToData();
        }

        private void ResetChartsZoom(object sender, RoutedEventArgs e) {
            positionChart.ResetZoom();
            robot1PositionChart.ResetZoom();
            robot2PositionChart.ResetZoom();
        }

        private void TakeChartScreenshot(object sender, RoutedEventArgs e) {
            if (!isPlotFrozen || robot1.IsInitialized() || robot2.IsInitialized()) {
                return;
            }

            string fileName = activeChart.YAxisTitle;

            if (string.IsNullOrEmpty(fileName)) {
                fileName = "screenshot.png";
            } else {
                fileName = fileName.ToLower().Replace(" ", "_") + ".png";
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog {
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "screenshots"),
                CheckPathExists = true,
                FilterIndex = 2,
                Title = "Save chart screenshot",
                DefaultExt = "png",
                Filter = "png files |*.png",
                FileName = fileName
            };

            if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileName != "") {
                int imageWidth = 800;

                using (MemoryStream imageStream = activeChart.ExportImage(imageWidth, (int)(imageWidth * 9.0 / 16.0))) {
                    File.WriteAllBytes(saveFileDialog.FileName, imageStream.ToArray());
                }
            }
        }

    }
}
