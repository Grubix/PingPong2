using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PingPong {
    public partial class RobotPanel : UserControl {

        private readonly TextBox[,] transformationTextBoxes;

        private ManualModeWindow manualModeWindow;

        private CalibrationWindow calibrationWindow;

        private LiveChart activeChart;

        private bool isPlotFrozen = false;

        public KUKARobot Robot { get; }

        public MainWindow MainWindowHandle { get; set; }

        public OptiTrackSystem OptiTrack { get; set; }

        public RobotPanel() {
            InitializeComponent();
            InitializeControls();
            InitializeCharts();

            Robot = new KUKARobot();
            InitializeRobot();

            transformationTextBoxes = new TextBox[,] {
                { t00, t01, t02, t03 },
                { t10, t11, t12, t13 },
                { t20, t21, t22, t23 },
                { t30, t31, t32, t33 }
            };

            calibrateBtn.IsEnabled = true;
            manualModeBtn.IsEnabled = true;
        }

        private void InitializeControls() {
            connectBtn.Click += Connect;
            disconnectBtn.Click += Disconnect;
            manualModeBtn.Click += OpenManualModeWindow;
            calibrateBtn.Click += OpenCalibrationWindow;
            loadConfigBtn.Click += LoadConfig;
            saveConfigBtn.Click += SaveConfig;

            freezeBtn.Click += FreezeCharts;
            fitToDataBtn.Click += FitChartsToData;
            resetZoomBtn.Click += ResetChartsZoom;
            screenshotBtn.Click += TakeChartScreenshot;

            // CTRL + S -> save active chart to png image
            Loaded += (s, e) => Focus();
            activeChart = positionChart;
            tabControl.SelectionChanged += (s, e) => {
                activeChart = (LiveChart)tabControl.SelectedContent;
            };
            KeyDown += (s, e) => {
                if (e.Key == Key.S && Keyboard.IsKeyDown(Key.LeftCtrl)) {
                    TakeChartScreenshot(null, null);
                }
            };
        }

        private void InitializeCharts() {
            positionChart.YAxisTitle = "Position (actual)";
            positionChart.AddSeries("Position X [mm]", "X", true);
            positionChart.AddSeries("Position Y [mm]", "Y", true);
            positionChart.AddSeries("Position Z [mm]", "Z", true);
            positionChart.AddSeries("Position A [deg]", "A", false);
            positionChart.AddSeries("Position B [deg]", "B", false);
            positionChart.AddSeries("Position C [deg]", "C", false);

            positionErrorChart.YAxisTitle = "Position error";
            positionErrorChart.AddSeries("Error X [mm]", "X", true);
            positionErrorChart.AddSeries("Error Y [mm]", "Y", true);
            positionErrorChart.AddSeries("Error Z [mm]", "Z", true);
            positionErrorChart.AddSeries("Error A [deg]", "A", false);
            positionErrorChart.AddSeries("Error B [deg]", "B", false);
            positionErrorChart.AddSeries("Error C [deg]", "C", false);

            velocityChart.YAxisTitle = "Velocity (theoretical)";
            velocityChart.AddSeries("Velocity X [mm/s]", "X", true);
            velocityChart.AddSeries("Velocity Y [mm/s]", "Y", true);
            velocityChart.AddSeries("Velocity Z [mm/s]", "Z", true);
            velocityChart.AddSeries("Velocity A [deg/s]", "A", false);
            velocityChart.AddSeries("Velocity B [deg/s]", "B", false);
            velocityChart.AddSeries("Velocity C [deg/s]", "C", false);

            accelerationChart.YAxisTitle = "Acceleration (theoretical)";
            accelerationChart.AddSeries("Acceleration X [mm/s²]", "X", true);
            accelerationChart.AddSeries("Acceleration Y [mm/s²]", "Y", true);
            accelerationChart.AddSeries("Acceleration Z [mm/s²]", "Z", true);
            accelerationChart.AddSeries("Acceleration A [deg/s²]", "A", false);
            accelerationChart.AddSeries("Acceleration B [deg/s²]", "B", false);
            accelerationChart.AddSeries("Acceleration C [deg/s²]", "C", false);
        }

        private void InitializeRobot() {
            Robot.Initialized += () => {
                Dispatcher.Invoke(() => {
                    calibrateBtn.IsEnabled = true;
                    manualModeBtn.IsEnabled = true;
                    ipAdress.Text = Robot.Ip;
                });
            };

            Robot.Uninitialized += () => {
                Dispatcher.Invoke(() => {
                    connectBtn.IsEnabled = true;
                    disconnectBtn.IsEnabled = false;
                    loadConfigBtn.IsEnabled = true;
                    saveConfigBtn.IsEnabled = true;
                });
            };

            Robot.FrameReceived += frame => {
                RobotVector actualPosition = Robot.Position;
                RobotVector targetPosition = Robot.TargetPosition;

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

                if (positionChart.IsReady) {
                    positionChart.Update(actualPosition.ToArray());
                } else {
                    positionChart.Tick();
                }

                if (positionErrorChart.IsReady) {
                    positionErrorChart.Update(Robot.PositionError.ToArray());
                } else {
                    positionErrorChart.Tick();
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

        private void Connect(object sender, RoutedEventArgs e) {
            try {
                if (Robot.IsInitialized()) {
                    return;
                }

                Robot.Config = CreateConfigurationFromFields();
                connectBtn.IsEnabled = false;
                disconnectBtn.IsEnabled = true;
                loadConfigBtn.IsEnabled = false;
                saveConfigBtn.IsEnabled = false;

                Robot.Initialize();
            } catch (Exception ex) {
                MainWindow.ShowErrorDialog("Robot initialization failed.", ex);
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e) {
            if (Robot.IsInitialized()) {
                Robot.Uninitialize();
            } else {
                connectBtn.IsEnabled = true;
                disconnectBtn.IsEnabled = false;
                loadConfigBtn.IsEnabled = true;
                saveConfigBtn.IsEnabled = true;
            }
        }

        private void OpenManualModeWindow(object sender, RoutedEventArgs e) {
            try {
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
            } catch (InvalidOperationException ex) {
                MainWindow.ShowErrorDialog("Unable to open manual mode window.", ex);
            }
        }

        private void OpenCalibrationWindow(object sender, RoutedEventArgs e) {
            try {
                if (calibrationWindow == null) {
                    calibrationWindow = new CalibrationWindow(Robot, OptiTrack);

                    if (MainWindowHandle != null) {
                        calibrationWindow.Owner = MainWindowHandle;
                    }

                    calibrationWindow.ProgressChanged += transformation => {
                        Robot.Config.Transformation = transformation;

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
            } catch (InvalidOperationException ex) {
                MainWindow.ShowErrorDialog("Unable to open calibration window.", ex);
            }
        }

        private void FreezeCharts(object sender, RoutedEventArgs e) {
            // MEGA WAZNE!!! 
            // freezowanie (a raczej zoomowanie i scrolowanie wykresu) moze powodowac opoznienia komunikacji z robotem co 
            // moze byc calkiem niebezpieczne, przykladowo opozniajac odbieranie ramek i tym samym spradzanie limitow robota.
            // Moze po kliknieciu w freeza zrobic diskonekta z robotami (oba musiałby by sie rozlaczyc)?

            if (isPlotFrozen) {
                positionChart.Clear();
                positionErrorChart.Clear();
                velocityChart.Clear();
                accelerationChart.Clear();

                positionChart.BlockZoomAndPan();
                positionErrorChart.BlockZoomAndPan();
                velocityChart.BlockZoomAndPan();
                accelerationChart.BlockZoomAndPan();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
                fitToDataBtn.IsEnabled = false;
                screenshotBtn.IsEnabled = false;
            } else {
                positionChart.UnblockZoomAndPan();
                positionErrorChart.UnblockZoomAndPan();
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
            positionErrorChart.FitToData();
            velocityChart.FitToData();
            accelerationChart.FitToData();
        }

        private void ResetChartsZoom(object sender, RoutedEventArgs e) {
            positionChart.ResetZoom();
            positionErrorChart.ResetZoom();
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

        private void LoadConfig(object sender, RoutedEventArgs e) {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog {
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "config"),
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
                    }
                } catch (Exception ex) {
                    MainWindow.ShowErrorDialog("Could not load configuration file.", ex);
                }
            }
        }

        private void SaveConfig(object sender, RoutedEventArgs e) {
            RobotConfig config = CreateConfigurationFromFields();

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog {
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "config"),
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
