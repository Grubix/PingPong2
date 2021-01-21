using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PingPong.Applications;
using PingPong.KUKA;
using PingPong.OptiTrack;

namespace PingPong {
    public partial class PingPongPanel : UserControl {

        private PingApp robot1PingApp;

        private PingApp robot2PingApp;

        private PingPongApp pingPongApp;

        private Robot robot1;

        private Robot robot2;

        private LiveChart activeChart;

        private bool isPlotFrozen = false;

        public PingPongPanel() {
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

            robot1PingApp = new PingApp(robot1, optiTrack, (position) => {
                return position[0] < 1000.0 && position[2] > 250.0;
            });

            robot1PingApp.DataReady += data => {
                if (isPlotFrozen) {
                    return;
                }

                if (robot1PingChart.IsReady) {
                    robot1PingChart.Update(new double[] {
                        data.PredictedTimeToHit,
                        data.BallSetpointX,
                        data.BallSetpointY,
                        data.BallSetpointZ,

                        data.ActualBallPosition[0],
                        data.PredictedBallPosition[0],
                        data.PredictedBallVelocity[0],

                        data.ActualBallPosition[1],
                        data.PredictedBallPosition[1],
                        data.PredictedBallVelocity[1],

                        data.ActualBallPosition[2],
                        data.PredictedBallPosition[2],
                        data.PredictedBallVelocity[2],

                        data.ActualRobotPosition.X,
                        data.ActualRobotPosition.Y,
                        data.ActualRobotPosition.Z,
                        data.ActualRobotPosition.A,
                        data.ActualRobotPosition.B,
                        data.ActualRobotPosition.C,
                    });
                } else {
                    robot1PingChart.Tick();
                }
            };

            // AUTOMATYCZNA INICJALIZACJA OPTITRACKA, ROBOTA I PINGA
            startBtn.Click += (s, e) => {
                try {
                    optiTrack.Initialize();
                    robot1.Initialize();
                    robot1.Initialized += () => robot1PingApp.Start();

                    startBtn.IsEnabled = false;
                } catch (Exception ex) {
                    MainWindow.ShowErrorDialog("Unable to start application.", ex);
                }
            };

            //robot2PingApp = new PingApp(robot2, optiTrack, (position) => {
            //    return position[0] < 1200.0; //TODO:
            //});

            //pingPongApp = new PingPongApp(robot1, robot2, optiTrack, (position) => {
            //    return true; //TODO:
            //});
        }

        private void InitializeCharts() {
            robot1PingChart.RefreshDelay = 30;
            robot1PingChart.YAxisTitle = "Ping app (robot 1)";

            robot1PingChart.AddSeries("Pred. time to hit [ms]", "T_Hpr", false);
            robot1PingChart.AddSeries("Ball target position X [mm]", "X_Bsp", false);
            robot1PingChart.AddSeries("Ball target position Y [mm]", "Y_Bsp", false);
            robot1PingChart.AddSeries("Ball target hit height [mm]", "Z_Bsp", false, true);

            robot1PingChart.AddSeries("Ball position X [mm]", "X_B", true);
            robot1PingChart.AddSeries("Ball pred. position X [mm]", "X_Bpr", true);
            robot1PingChart.AddSeries("Ball pred. velocity X [mm/s]", "V_XBpr", false, true);

            robot1PingChart.AddSeries("Ball position Y [mm]", "Y_B", true);
            robot1PingChart.AddSeries("Ball pred. position Y [mm]", "Y_Bpr", true);
            robot1PingChart.AddSeries("Ball pred. velocity Y [mm/s]", "V_YBpr", false, true);

            robot1PingChart.AddSeries("Ball position Z [mm]", "Z_B", true);
            robot1PingChart.AddSeries("Ball pred. position Z [mm]", "Z_Bpr", true);
            robot1PingChart.AddSeries("Ball pred. velocity Z [mm/s]", "V_ZBpr", false, true);

            robot1PingChart.AddSeries("Robot position X [mm]", "X_R", false);
            robot1PingChart.AddSeries("Robot position Y [mm]", "Y_R", false);
            robot1PingChart.AddSeries("Robot position Z [mm]", "Z_R", false);
            robot1PingChart.AddSeries("Robot position A [deg]", "A_R", false);
            robot1PingChart.AddSeries("Robot position B [deg]", "B_R", false);
            robot1PingChart.AddSeries("Robot position C [deg]", "C_R", false);
        }

        private void InitializeControls() {
            freezeBtn.Click += FreezeCharts;
            fitToDataBtn.Click += FitChartsToData;
            resetZoomBtn.Click += ResetChartsZoom;
            screenshotBtn.Click += TakeChartScreenshot;

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

        private void FreezeCharts(object sender, RoutedEventArgs e) {
            if (isPlotFrozen) {
                robot1PingChart.Clear();
                robot2PingChart.Clear();
                pingPongChart.Clear();

                robot1PingChart.BlockZoomAndPan();
                robot2PingChart.BlockZoomAndPan();
                pingPongChart.BlockZoomAndPan();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
                fitToDataBtn.IsEnabled = false;
                screenshotBtn.IsEnabled = false;
            } else {
                robot1PingChart.UnblockZoomAndPan();
                robot2PingChart.UnblockZoomAndPan();
                pingPongChart.UnblockZoomAndPan();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
                screenshotBtn.IsEnabled = true;

                FitChartsToData(null, null);
            }
        }

        private void FitChartsToData(object sender, RoutedEventArgs e) {
            robot1PingChart.FitToData();
            robot2PingChart.FitToData();
            pingPongChart.FitToData();
        }

        private void ResetChartsZoom(object sender, RoutedEventArgs e) {
            robot1PingChart.ResetZoom();
            robot2PingChart.ResetZoom();
            pingPongChart.ResetZoom();
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
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots"),
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