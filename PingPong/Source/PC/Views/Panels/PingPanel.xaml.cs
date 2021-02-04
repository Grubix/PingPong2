using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PingPong.Applications;
using PingPong.KUKA;
using PingPong.OptiTrack;

namespace PingPong {
    public partial class PingPanel : UserControl {

        private PingApp robot1PingApp;

        private Robot robot1;

        private Robot robot2;

        private OptiTrackSystem optiTrack;

        private LiveChart activeChart;

        private bool isPlotFrozen = false;

        public event Action Started;

        public event Action Stopped;

        public PingPanel() {
            InitializeComponent();
            InitializeCharts();
            InitializeControls();
        }

        public void Initialize(Robot robot1, Robot robot2, OptiTrackSystem optiTrack) {
            if (robot1 == robot2) {
                throw new ArgumentException("Cos tam ze instancje robota musza byc rozne");
            }

            this.robot1 = robot1;
            this.robot2 = robot2;
            this.optiTrack = optiTrack;

            robot1PingApp = new PingApp(robot1, optiTrack, (ballPosition) => {
                return ballPosition[0] < 1000.0 && ballPosition[2] > 600.0;
            });

            robot1PingApp.DataReady += UpdateUI;

            robot1PingApp.Started += (s, e) => {
                startBtn.IsEnabled = false;
                stopBtn.IsEnabled = true;
                copyMovementsCheck.IsEnabled = false;

                Started?.Invoke();
            };

            robot1PingApp.Stopped += (s, e) => {
                startBtn.IsEnabled = true;
                stopBtn.IsEnabled = false;
                copyMovementsCheck.IsEnabled = true;

                optiTrack.Uninitialize();
                robot1.Uninitialize();
                robot2.Uninitialize();

                Stopped?.Invoke();
            };

            robot1.Initialized += (s, e) => {
                if ((bool)copyMovementsCheck.IsChecked) {
                    if (robot2.IsInitialized()) {
                        robot1PingApp.Start();
                    }
                } else {
                    robot1PingApp.Start();
                }
            };

            robot2.Initialized += (s, e) => {
                if ((bool)copyMovementsCheck.IsChecked) {
                    if (robot1.IsInitialized()) {
                        robot1PingApp.Start();
                    }
                }
            };
        }

        private void CopyMovements() {
            //TODO:
        }

        public void DisableUIUpdates() {
            robot1PingApp.DataReady -= UpdateUI;
        }

        public void EnableUIUpdates() {
            robot1PingApp.DataReady += UpdateUI;
        }

        private void UpdateUI(object sender, PingDataReadyEventArgs args) {
            robot1PingChart.Update(new double[] {
                args.PredictedTimeToHit,
                args.ActualBallPosition[0], args.PredictedBallPosition[0],
                args.ActualBallPosition[1], args.PredictedBallPosition[1],
                args.ActualBallPosition[2], args.PredictedBallPosition[2],
                args.ActualRobotPosition.X, args.ActualRobotPosition.Y, args.ActualRobotPosition.Z,
                args.ActualRobotPosition.A, args.ActualRobotPosition.B, args.ActualRobotPosition.C
            });
        }

        private void InitializeCharts() {
            robot1PingChart.Title = "Ping app";

            robot1PingChart.AddSeries("Pred. time to hit [s]", "T_Hpr", false, true);

            robot1PingChart.AddSeries("Ball position X [mm]", "X_B", true);
            robot1PingChart.AddSeries("Ball pred. position X [mm]", "X_Bpr", false, true);

            robot1PingChart.AddSeries("Ball position Y [mm]", "Y_B", true);
            robot1PingChart.AddSeries("Ball pred. position Y [mm]", "Y_Bpr", false, true);

            robot1PingChart.AddSeries("Ball position Z [mm]", "Z_B", true);
            robot1PingChart.AddSeries("Ball pred. position Z [mm]", "Z_Bpr", false, true);

            robot1PingChart.AddSeries("Robot position X [mm]", "X_R", false);
            robot1PingChart.AddSeries("Robot position Y [mm]", "Y_R", false);
            robot1PingChart.AddSeries("Robot position Z [mm]", "Z_R", true);
            robot1PingChart.AddSeries("Robot position A [deg]", "A_R", false);
            robot1PingChart.AddSeries("Robot position B [deg]", "B_R", false);
            robot1PingChart.AddSeries("Robot position C [deg]", "C_R", false);
        }

        private void InitializeControls() {
            freezeBtn.Click += FreezeCharts;
            fitToDataBtn.Click += FitChartsToData;
            resetZoomBtn.Click += ResetChartsZoom;
            screenshotBtn.Click += TakeChartScreenshot;

            startBtn.Click += Start;
            stopBtn.Click += Stop;

            //TODO: settery

            activeChart = robot1PingChart;
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

        private void Start(object sender, RoutedEventArgs e) {
            try {
                if (!optiTrack.IsInitialized()) {
                    optiTrack.Initialize();
                }

                if (!robot1.IsInitialized()) {
                    robot1.Initialize();
                }

                if ((bool)copyMovementsCheck.IsChecked && !robot2.IsInitialized()) {
                    robot2.Initialize();
                }

                robot1PingApp.Start();

                startBtn.IsEnabled = false;
                stopBtn.IsEnabled = true;
                copyMovementsCheck.IsEnabled = false;
            } catch (Exception ex) {
                MainWindow.ShowErrorDialog("Cannot start application.", ex);
            }
        }

        private void Stop(object sender, RoutedEventArgs e) {
            robot1PingApp.Stop();
            startBtn.IsEnabled = true;
            stopBtn.IsEnabled = false;
            copyMovementsCheck.IsEnabled = true;
        }

        private void FreezeCharts(object sender, RoutedEventArgs e) {
            if (isPlotFrozen) {
                robot1PingChart.Unfreeze();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
                fitToDataBtn.IsEnabled = false;
                screenshotBtn.IsEnabled = false;
            } else {
                robot1PingChart.Freeze();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
                screenshotBtn.IsEnabled = true;

                FitChartsToData(null, null);
            }
        }

        public void ForceFreezeCharts() {
            Dispatcher.Invoke(() => {
                isPlotFrozen = true;

                robot1PingChart.Freeze();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
                screenshotBtn.IsEnabled = true;

                FitChartsToData(null, null);
            });
        }

        private void FitChartsToData(object sender, RoutedEventArgs e) {
            robot1PingChart.FitToData();
        }

        private void ResetChartsZoom(object sender, RoutedEventArgs e) {
            robot1PingChart.ResetZoom();
        }

        private void TakeChartScreenshot(object sender, RoutedEventArgs e) {
            if (!isPlotFrozen) {
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