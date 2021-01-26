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
            OptiTrack.Initialized += (s, e) => {
                hostApp.Text = OptiTrack.ServerDescription.HostApp;
                hostName.Text = OptiTrack.ServerDescription.HostComputerName;
                hostAdress.Text = OptiTrack.ServerDescription.HostComputerAddress.ToString(); //TODO:
                natnetVersion.Text = OptiTrack.ServerDescription.NatNetVersion.ToString(); //TODO:

                connectBtn.IsEnabled = false;
                disconnectBtn.IsEnabled = true;
            };
            OptiTrack.Uninitialized += (s, e) => {
                Dispatcher.Invoke(() => {
                    connectBtn.IsEnabled = true;
                    disconnectBtn.IsEnabled = false;
                });
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

            robot1.Initialized += (s, e) => robot1Transformation = robot1.OptiTrackTransformation;
            robot2.Initialized += (s, e) => robot2Transformation = robot2.OptiTrackTransformation;
        }

        private void UpdateOptiTrackBasePositionChart(object sender, OptiTrack.FrameReceivedEventArgs args) {
            positionChart.Update(args.BallPosition.ToArray());
            Dispatcher.Invoke(() => {
                actualPositionX.Text = args.BallPosition[0].ToString("F3");
                actualPositionY.Text = args.BallPosition[1].ToString("F3");
                actualPositionZ.Text = args.BallPosition[2].ToString("F3");
            });
        }

        private void UpdateRobot1BasePositionChart(object sender, OptiTrack.FrameReceivedEventArgs args) {
            var robot1BasePosition = robot1Transformation.Convert(args.BallPosition);

            robot1PositionChart.Update(new double[] {
                robot1BasePosition[0], robot1BasePosition[1], robot1BasePosition[2]
            });
            Dispatcher.Invoke(() => {
                robot1BaseActualPositionX.Text = robot1BasePosition[0].ToString("F3");
                robot1BaseActualPositionY.Text = robot1BasePosition[1].ToString("F3");
                robot1BaseActualPositionZ.Text = robot1BasePosition[2].ToString("F3");
            });
        }

        private void UpdateRobot2BasePositionChart(object sender, OptiTrack.FrameReceivedEventArgs args) {
            var robot2BasePosition = robot2Transformation.Convert(args.BallPosition);
            
            robot2PositionChart.Update(new double[] {
                robot2BasePosition[0], robot2BasePosition[1], robot2BasePosition[2]
            });
            Dispatcher.Invoke(() => {
                robot2BaseActualPositionX.Text = robot2BasePosition[0].ToString("F3");
                robot2BaseActualPositionY.Text = robot2BasePosition[1].ToString("F3");
                robot2BaseActualPositionZ.Text = robot2BasePosition[2].ToString("F3");
            });
        }

        private void InitializeCharts() {
            positionChart.Title = "Position(OptiTrack base)";
            positionChart.AddSeries("Ball position X [mm]", "X", true);
            positionChart.AddSeries("Ball position Y [mm]", "Y", true);
            positionChart.AddSeries("Ball position Z [mm]", "Z", true);

            robot1PositionChart.Title = "Position (robot1 base)";
            robot1PositionChart.AddSeries("Robot 1 base ball position X [mm]", "X", true);
            robot1PositionChart.AddSeries("Robot 1 base ball position Y [mm]", "Y", true);
            robot1PositionChart.AddSeries("Robot 1 base ball position Z [mm]", "Z", true);

            robot2PositionChart.Title = "Position (robot2 base)";
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
            } catch (InvalidOperationException ex) {
                MainWindow.ShowErrorDialog("OptiTrack system initialization failed.", ex);
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e) {
            OptiTrack.Uninitialize();

            OptiTrack.FrameReceived -= UpdateOptiTrackBasePositionChart;
            OptiTrack.FrameReceived -= UpdateRobot1BasePositionChart;
            OptiTrack.FrameReceived -= UpdateRobot2BasePositionChart;
        }

        private void FreezeCharts(object sender, RoutedEventArgs e) {
            if (isPlotFrozen) {
                positionChart.Unfreeze();
                robot1PositionChart.Unfreeze();
                robot2PositionChart.Unfreeze();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
                fitToDataBtn.IsEnabled = false;
                screenshotBtn.IsEnabled = false;
            } else {
                positionChart.Freeze();
                robot1PositionChart.Freeze();
                robot2PositionChart.Freeze();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
                screenshotBtn.IsEnabled = true;

                FitChartsToData(null, null);
            }
        }

        public void ForceFreezeCharts() {
            isPlotFrozen = true;

            positionChart.Freeze();
            robot1PositionChart.Freeze();
            robot2PositionChart.Freeze();

            isPlotFrozen = true;
            freezeBtn.Content = "Unfreeze";
            resetZoomBtn.IsEnabled = true;
            fitToDataBtn.IsEnabled = true;
            screenshotBtn.IsEnabled = true;

            FitChartsToData(null, null);
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

            string fileName = activeChart.Title;

            if (string.IsNullOrEmpty(fileName)) {
                fileName = "screenshot.png";
            } else {
                fileName = fileName.ToLower().Replace(" ", "_") + ".png";
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog {
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), App.ScreenshotsDir),
                CheckPathExists = true,
                FilterIndex = 2,
                Title = "Save chart screenshot",
                DefaultExt = "png",
                Filter = "png files |*.png",
                FileName = fileName
            };

            if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileName != "") {
                int imageWidth = 800;

                using (MemoryStream imageStream = activeChart.ExportPng(imageWidth, (int)(imageWidth * 9.0 / 16.0))) {
                    File.WriteAllBytes(saveFileDialog.FileName, imageStream.ToArray());
                }
            }
        }

    }
}
