using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PingPong {
    public partial class RobotPanel : UserControl {

        private ManualModeWindow manualModeWindow;

        private CalibrationWindow calibrationWindow;

        private LiveChart activeChart;

        private bool isPlotFrozen = false;

        public Robot Robot { get; }

        public MainWindow MainWindowHandle { get; set; }

        public OptiTrackSystem OptiTrack { get; set; }

        public RobotPanel() {
            InitializeComponent();
            InitializeControls();
            InitializeCharts();

            Robot = new Robot();
            InitializeRobot();
        }

        public void LoadConfig(string configFile) {
            string jsonString = File.ReadAllText(configFile);
            RobotConfig config = new RobotConfig(jsonString);

            Robot.Config = config;
            UpdateConfigControls(config);
        }

        private void InitializeControls() {
            initializeBtn.Click += Initialize;
            disconnectBtn.Click += Disconnect;
            manualModeBtn.Click += OpenManualModeWindow;
            calibrateBtn.Click += OpenCalibrationWindow;
            loadConfigBtn.Click += LoadConfig;
            saveConfigBtn.Click += SaveConfig;

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

        private void InitializeCharts() {
            positionChart.YAxisTitle = "Position";
            positionChart.AddSeries("Actual position X [mm]", "X", true);
            positionChart.AddSeries("Target position X [mm]", "X_T", false);
            positionChart.AddSeries("Theoretical position X [mm]", "X_TH", false, true);
            positionChart.AddSeries("Actual position Y [mm]", "Y", true);
            positionChart.AddSeries("Target position Y [mm]", "Y_T", false);
            positionChart.AddSeries("Theoretical position Y [mm]", "Y_TH", false, true);
            positionChart.AddSeries("Actual position Z [mm]", "Z", true);
            positionChart.AddSeries("Target position Z [mm]", "Z_T", false);
            positionChart.AddSeries("Theoretical position Z [mm]", "Z_TH", false, true);
            positionChart.AddSeries("Actual position A [deg]", "A", false);
            positionChart.AddSeries("Target position A [mm]", "A_T", false);
            positionChart.AddSeries("Theoretical position A [deg]", "A_TH", false, true);
            positionChart.AddSeries("Actual position B [deg]", "B", false);
            positionChart.AddSeries("Target position B [deg]", "B_T", false);
            positionChart.AddSeries("Theoretical position B [deg]", "B_TH", false, true);
            positionChart.AddSeries("Actual position C [deg]", "C", false);
            positionChart.AddSeries("Target position C [deg]", "C_T", false);
            positionChart.AddSeries("Theoretical position C [deg]", "C_TH", false);

            velocityChart.YAxisTitle = "Velocity (theoretical)";
            velocityChart.AddSeries("Velocity X [mm/s]", "V_X", true);
            velocityChart.AddSeries("Velocity Y [mm/s]", "V_Y", true);
            velocityChart.AddSeries("Velocity Z [mm/s]", "V_Z", true);
            velocityChart.AddSeries("Velocity A [deg/s]", "V_A", false);
            velocityChart.AddSeries("Velocity B [deg/s]", "V_B", false);
            velocityChart.AddSeries("Velocity C [deg/s]", "V_C", false);

            accelerationChart.YAxisTitle = "Acceleration (theoretical)";
            accelerationChart.AddSeries("Acceleration X [mm/s²]", "A_X", true);
            accelerationChart.AddSeries("Acceleration Y [mm/s²]", "A_Y", true);
            accelerationChart.AddSeries("Acceleration Z [mm/s²]", "A_Z", true);
            accelerationChart.AddSeries("Acceleration A [deg/s²]", "A_A", false);
            accelerationChart.AddSeries("Acceleration B [deg/s²]", "A_B", false);
            accelerationChart.AddSeries("Acceleration C [deg/s²]", "A_C", false);
        }

        private void InitializeRobot() {
            Robot.Initialized += () => {
                Dispatcher.Invoke(() => {
                    initializeBtn.IsEnabled = false;
                    disconnectBtn.IsEnabled = true;
                    calibrateBtn.IsEnabled = true;
                    manualModeBtn.IsEnabled = true;
                    loadConfigBtn.IsEnabled = false;
                    saveConfigBtn.IsEnabled = false;

                    ipAdress.Text = Robot.Ip;
                    homePositionX.Text = Robot.HomePosition.X.ToString("F3");
                    homePositionY.Text = Robot.HomePosition.Y.ToString("F3");
                    homePositionZ.Text = Robot.HomePosition.Z.ToString("F3");
                    homePositionA.Text = Robot.HomePosition.A.ToString("F3");
                    homePositionB.Text = Robot.HomePosition.B.ToString("F3");
                    homePositionC.Text = Robot.HomePosition.C.ToString("F3");
                });
            };

            Robot.Uninitialized += () => {
                Dispatcher.Invoke(() => {
                    initializeBtn.IsEnabled = true;
                    disconnectBtn.IsEnabled = false;
                    loadConfigBtn.IsEnabled = true;
                    saveConfigBtn.IsEnabled = true;
                });
            };

            Robot.FrameReceived += frame => {
                if (isPlotFrozen) {
                    return;
                }

                if (positionChart.IsReady) {
                    RobotVector actualPosition = Robot.Position;
                    RobotVector targetPosition = Robot.TargetPosition;
                    RobotVector theoreticalPosition = Robot.TheoreticalPosition;

                    positionChart.Update(new double[] {
                        actualPosition.X, targetPosition.X, theoreticalPosition.X,
                        actualPosition.Y, targetPosition.Y, theoreticalPosition.Y,
                        actualPosition.Z, targetPosition.Z, theoreticalPosition.Z,
                        actualPosition.A, targetPosition.A, theoreticalPosition.A,
                        actualPosition.B, targetPosition.B, theoreticalPosition.B,
                        actualPosition.C, targetPosition.C, theoreticalPosition.C,
                    });

                    Dispatcher.Invoke(() => {
                        actualPositionX.Text = actualPosition.X.ToString("F3");
                        actualPositionY.Text = actualPosition.Y.ToString("F3");
                        actualPositionZ.Text = actualPosition.Z.ToString("F3");
                        actualPositionA.Text = actualPosition.A.ToString("F3");
                        actualPositionB.Text = actualPosition.B.ToString("F3");
                        actualPositionC.Text = actualPosition.C.ToString("F3");

                        targetPositionX.Text = targetPosition.X.ToString("F3");
                        targetPositionY.Text = targetPosition.Y.ToString("F3");
                        targetPositionZ.Text = targetPosition.Z.ToString("F3");
                        targetPositionA.Text = targetPosition.A.ToString("F3");
                        targetPositionB.Text = targetPosition.B.ToString("F3");
                        targetPositionC.Text = targetPosition.C.ToString("F3");
                    });
                } else {
                    positionChart.Tick();
                }

                if (velocityChart.IsReady) {
                    velocityChart.Update(Robot.Velocity.ToArray());
                } else {
                    velocityChart.Tick();
                }

                if (accelerationChart.IsReady) {
                    accelerationChart.Update(Robot.Acceleration.ToArray());
                } else {
                    accelerationChart.Tick();
                }
            };
        }

        private void Initialize(object sender, RoutedEventArgs e) {
            try {
                if (Robot.IsInitialized()) {
                    return;
                }

                Robot.Config = CreateConfigurationFromFields();
                Robot.Initialize();

                initializeBtn.IsEnabled = false;
                loadConfigBtn.IsEnabled = false;
                saveConfigBtn.IsEnabled = false;
                disconnectBtn.IsEnabled = true;
            } catch (Exception ex) {
                MainWindow.ShowErrorDialog("Robot initialization failed.", ex);
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e) {
            Robot.Uninitialize();

            if (!Robot.IsInitialized()) {
                initializeBtn.IsEnabled = true;
                disconnectBtn.IsEnabled = false;
                manualModeBtn.IsEnabled = false;
                calibrateBtn.IsEnabled = false;
                loadConfigBtn.IsEnabled = true;
                saveConfigBtn.IsEnabled = true;
            }
        }

        private void OpenManualModeWindow(object sender, RoutedEventArgs e) {
            if (manualModeWindow == null) {
                manualModeWindow = new ManualModeWindow(Robot);

                if (MainWindowHandle != null) {
                    manualModeWindow.Owner = MainWindowHandle;
                }

                manualModeWindow.Closed += (se, ev) => manualModeWindow = null;
                manualModeWindow.Show();
            } else {
                manualModeWindow.WindowState = WindowState.Normal;
                manualModeWindow.Activate();
            }
        }

        private void OpenCalibrationWindow(object sender, RoutedEventArgs e) {
            if (calibrationWindow == null) {
                calibrationWindow = new CalibrationWindow(Robot, OptiTrack);

                if (MainWindowHandle != null) {
                    calibrationWindow.Owner = MainWindowHandle;
                }

                calibrationWindow.Completed += transformation => {
                    Disconnect(null, null);

                    Dispatcher.Invoke(() => {
                        t00.Text = transformation[0, 0].ToString("F3");
                        t01.Text = transformation[0, 1].ToString("F3");
                        t02.Text = transformation[0, 2].ToString("F3");
                        t03.Text = transformation[0, 3].ToString("F3");

                        t10.Text = transformation[1, 0].ToString("F3");
                        t11.Text = transformation[1, 1].ToString("F3");
                        t12.Text = transformation[1, 2].ToString("F3");
                        t13.Text = transformation[1, 3].ToString("F3");

                        t20.Text = transformation[2, 0].ToString("F3");
                        t21.Text = transformation[2, 1].ToString("F3");
                        t22.Text = transformation[2, 2].ToString("F3");
                        t23.Text = transformation[2, 3].ToString("F3");

                        t30.Text = transformation[3, 0].ToString("F3");
                        t31.Text = transformation[3, 1].ToString("F3");
                        t32.Text = transformation[3, 2].ToString("F3");
                        t33.Text = transformation[3, 3].ToString("F3");
                    });
                };

                calibrationWindow.Closed += (se, ev) => calibrationWindow = null;
                calibrationWindow.Show();
            } else {
                calibrationWindow.WindowState = WindowState.Normal;
                calibrationWindow.Activate();
            }
        }

        private void FreezeCharts(object sender, RoutedEventArgs e) {
            if (isPlotFrozen) {
                positionChart.Clear();
                velocityChart.Clear();
                accelerationChart.Clear();

                positionChart.BlockZoomAndPan();
                velocityChart.BlockZoomAndPan();
                accelerationChart.BlockZoomAndPan();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
                fitToDataBtn.IsEnabled = false;
                screenshotBtn.IsEnabled = false;
            } else {
                positionChart.UnblockZoomAndPan();
                velocityChart.UnblockZoomAndPan();
                accelerationChart.UnblockZoomAndPan();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
                screenshotBtn.IsEnabled = true;
            }
        }

        private void FitChartsToData(object sender, RoutedEventArgs e) {
            positionChart.FitToData();
            velocityChart.FitToData();
            accelerationChart.FitToData();
        }

        private void ResetChartsZoom(object sender, RoutedEventArgs e) {
            positionChart.ResetZoom();
            velocityChart.ResetZoom();
            accelerationChart.ResetZoom();
        }

        private void TakeChartScreenshot(object sender, RoutedEventArgs e) {
            if (!isPlotFrozen || Robot.IsInitialized()) {
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

        private void LoadConfig(object sender, RoutedEventArgs e) {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog {
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Config"),
                Title = "Select configuration file",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "JSON files |*.json",
                FilterIndex = 2,
                ReadOnlyChecked = true,
                ShowReadOnly = true,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true) {
                Stream fileStream;
                StreamReader streamReader;

                try {
                    if ((fileStream = openFileDialog.OpenFile()) != null) {
                        using (fileStream)
                        using (streamReader = new StreamReader(fileStream)) {
                            string jsonString = streamReader.ReadToEnd();
                            RobotConfig config = new RobotConfig(jsonString);
                            Robot.Config = config;

                            UpdateConfigControls(config);
                        }
                    }
                } catch (Exception ex) {
                    MainWindow.ShowErrorDialog("Could not load configuration file.", ex);
                }
            }
        }

        private void SaveConfig(object sender, RoutedEventArgs e) {
            RobotConfig config = CreateConfigurationFromFields();

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog {
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Config"),
                CheckPathExists = true,
                FilterIndex = 2,
                Title = "Save configuration file",
                DefaultExt = "json",
                Filter = "JSON files |*.json",
                FileName = "robot.config.json"
            };

            if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileName != "") {
                File.WriteAllText(saveFileDialog.FileName, config.ToJsonString());
            }
        }

        private void UpdateConfigControls(RobotConfig config) {
            connectionPort.Text = config.Port.ToString();

            workspaceLowerX.Text = config.Limits.LowerWorkspacePoint.X.ToString();
            workspaceLowerY.Text = config.Limits.LowerWorkspacePoint.Y.ToString();
            workspaceLowerZ.Text = config.Limits.LowerWorkspacePoint.Z.ToString();

            workspaceUpperX.Text = config.Limits.UpperWorkspacePoint.X.ToString();
            workspaceUpperY.Text = config.Limits.UpperWorkspacePoint.Y.ToString();
            workspaceUpperZ.Text = config.Limits.UpperWorkspacePoint.Z.ToString();

            a1LowerLimit.Text = config.Limits.A1AxisLimit.Min.ToString();
            a1UpperLimit.Text = config.Limits.A1AxisLimit.Max.ToString();
            a2LowerLimit.Text = config.Limits.A2AxisLimit.Min.ToString();
            a2UpperLimit.Text = config.Limits.A2AxisLimit.Max.ToString();
            a3LowerLimit.Text = config.Limits.A3AxisLimit.Min.ToString();
            a3UpperLimit.Text = config.Limits.A3AxisLimit.Max.ToString();
            a4LowerLimit.Text = config.Limits.A4AxisLimit.Min.ToString();
            a4UpperLimit.Text = config.Limits.A4AxisLimit.Max.ToString();
            a5LowerLimit.Text = config.Limits.A5AxisLimit.Min.ToString();
            a5UpperLimit.Text = config.Limits.A5AxisLimit.Max.ToString();
            a6LowerLimit.Text = config.Limits.A6AxisLimit.Min.ToString();
            a6UpperLimit.Text = config.Limits.A6AxisLimit.Max.ToString();

            correctionLimitXYZ.Text = config.Limits.CorrectionLimit.XYZ.ToString();
            correctionLimitABC.Text = config.Limits.CorrectionLimit.ABC.ToString();

            t00.Text = config.Transformation[0, 0].ToString("F3");
            t01.Text = config.Transformation[0, 1].ToString("F3");
            t02.Text = config.Transformation[0, 2].ToString("F3");
            t03.Text = config.Transformation[0, 3].ToString("F3");

            t10.Text = config.Transformation[1, 0].ToString("F3");
            t11.Text = config.Transformation[1, 1].ToString("F3");
            t12.Text = config.Transformation[1, 2].ToString("F3");
            t13.Text = config.Transformation[1, 3].ToString("F3");

            t20.Text = config.Transformation[2, 0].ToString("F3");
            t21.Text = config.Transformation[2, 1].ToString("F3");
            t22.Text = config.Transformation[2, 2].ToString("F3");
            t23.Text = config.Transformation[2, 3].ToString("F3");

            t30.Text = config.Transformation[3, 0].ToString("F3");
            t31.Text = config.Transformation[3, 1].ToString("F3");
            t32.Text = config.Transformation[3, 2].ToString("F3");
            t33.Text = config.Transformation[3, 3].ToString("F3");
        }

        private RobotConfig CreateConfigurationFromFields() {
            int port = int.Parse(connectionPort.Text);

            RobotLimits limits = new RobotLimits(
                (double.Parse(workspaceLowerX.Text), double.Parse(workspaceLowerY.Text), double.Parse(workspaceLowerZ.Text)),
                (double.Parse(workspaceUpperX.Text), double.Parse(workspaceUpperY.Text), double.Parse(workspaceUpperZ.Text)),
                (double.Parse(a1LowerLimit.Text), double.Parse(a1UpperLimit.Text)),
                (double.Parse(a2LowerLimit.Text), double.Parse(a2UpperLimit.Text)),
                (double.Parse(a3LowerLimit.Text), double.Parse(a3UpperLimit.Text)),
                (double.Parse(a4LowerLimit.Text), double.Parse(a4UpperLimit.Text)),
                (double.Parse(a5LowerLimit.Text), double.Parse(a5UpperLimit.Text)),
                (double.Parse(a6LowerLimit.Text), double.Parse(a6UpperLimit.Text)),
                (double.Parse(correctionLimitXYZ.Text), double.Parse(correctionLimitABC.Text))
            );

            var rotation = Matrix<double>.Build.DenseOfArray(new double[,] {
                { double.Parse(t00.Text), double.Parse(t01.Text), double.Parse(t02.Text) },
                { double.Parse(t10.Text), double.Parse(t11.Text), double.Parse(t12.Text) },
                { double.Parse(t20.Text), double.Parse(t21.Text), double.Parse(t22.Text) },
            });

            var translation = Vector<double>.Build.DenseOfArray(new double[] {
                double.Parse(t03.Text), double.Parse(t13.Text), double.Parse(t23.Text)
            });

            Transformation transformation = new Transformation(rotation, translation);

            return new RobotConfig(port, limits, transformation);
        }

    }
}
