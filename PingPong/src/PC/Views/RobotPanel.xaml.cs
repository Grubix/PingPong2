using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public KUKARobot Robot { get; } = new KUKARobot();

        public MainWindow MainWindowHandle { get; set; }

        public OptiTrackSystem OptiTrack { get; set; }

        public RobotPanel() {
            InitializeComponent();
            InitializeControls();
            InitializeCharts();
            InitializeRobot();

            transformationTextBoxes = new TextBox[,] {
                { t00, t01, t02, t03 },
                { t10, t11, t12, t13 },
                { t20, t21, t22, t23 },
                { t30, t31, t32, t33 }
            };

            RobotVector actualPosition = new RobotVector();
            RobotVector targetPosition = new RobotVector(50, -150, 300);
            TrajectoryGenerator5v2 generator = new TrajectoryGenerator5v2();

            generator.Restart(actualPosition);
            generator.SetTargetPosition(actualPosition, targetPosition, new RobotVector(-50, 50, -50), 18);

            Task.Run(() => {
                for (int i = 0; i < 10000; i++) {
                    Thread.Sleep(4);
                    actualPosition = generator.GetNextAbsoluteCorrection(actualPosition);

                    if (isPlotFrozen) {
                        continue;
                    }

                    if (positionChart.IsReady) {
                        positionChart.Update(actualPosition.ToArray());
                    } else {
                        positionChart.Tick();
                    }

                    if (positionErrorChart.IsReady) {
                        positionErrorChart.Update(generator.PositionError.ToArray());
                    } else {
                        positionErrorChart.Tick();
                    }

                    if (velocityChart.IsReady) {
                        velocityChart.Update(generator.Velocity.ToArray());
                    } else {
                        velocityChart.Tick();
                    }

                    if (accelerationChart.IsReady) {
                        accelerationChart.Update(generator.Acceleration.ToArray());
                    } else {
                        accelerationChart.Tick();
                    }
                }
            });
        }

        private void InitializeControls() {
            connectBtn.Click += Connect;
            disconnectBtn.Click += Disconnect;
            manualModeBtn.Click += OpenManualModeWindow;
            calibrateBtn.Click += OpenCalibrationWindow;
            loadConfigBtn.Click += LoadConfig;
            saveConfigBtn.Click += (s, e) => CreateConfiguration().SaveToFile();

            freezeBtn.Click += FreezeOrUnfreeze;
            fitToDataBtn.Click += FitChartsToData;
            resetZoomBtn.Click += ResetChartsZoom;

            Loaded += (s, e) => Focus();
            activeChart = positionChart;
            tabControl.SelectionChanged += (s, e) => {
                activeChart = (LiveChart)tabControl.SelectedContent;
            };
            KeyDown += (s, e) => {
                if (isPlotFrozen && !Robot.IsInitialized() && e.Key == Key.S && Keyboard.IsKeyDown(Key.LeftCtrl)) {
                    activeChart.SaveToImage(800, (int)(800 * 9.0 / 16.0));
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
                RobotVector position = Robot.Position;
                RobotVector targetPosition = Robot.TargetPosition;

                Dispatcher.Invoke(() => {
                    actualPositionX.Text = position.X.ToString("F3");
                    actualPositionY.Text = position.Y.ToString("F3");
                    actualPositionZ.Text = position.Z.ToString("F3");
                    actualPositionA.Text = position.A.ToString("F3");
                    actualPositionB.Text = position.B.ToString("F3");
                    actualPositionC.Text = position.C.ToString("F3");

                    targetPositionX.Text = targetPosition.X.ToString("F3");
                    targetPositionY.Text = targetPosition.Y.ToString("F3");
                    targetPositionZ.Text = targetPosition.Z.ToString("F3");
                    targetPositionA.Text = targetPosition.A.ToString("F3");
                    targetPositionB.Text = targetPosition.B.ToString("F3");
                    targetPositionC.Text = targetPosition.C.ToString("F3");
                });

                if (positionChart.IsReady) {
                    positionChart.Update(position.ToArray());
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

                Robot.Config = CreateConfiguration();
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
            if (manualModeWindow == null) {
                manualModeWindow = new ManualModeWindow(Robot);

                if (MainWindowHandle != null) {
                    manualModeWindow.Owner = MainWindowHandle;
                }

                manualModeWindow.Closed += (se, ev) => manualModeWindow = null;
                manualModeWindow.Show();
            } else {
                manualModeWindow.Activate();
            }
        }

        private void OpenCalibrationWindow(object sender, RoutedEventArgs e) {
            if (calibrationWindow == null) {
                calibrationWindow = new CalibrationWindow(Robot, OptiTrack, transformationTextBoxes);

                if (MainWindowHandle != null) {
                    calibrationWindow.Owner = MainWindowHandle;
                }

                calibrationWindow.Closed += (se, ev) => calibrationWindow = null;
                calibrationWindow.Show();
            } else {
                calibrationWindow.Activate();
            }
        }

        private void FreezeOrUnfreeze(object sender, RoutedEventArgs e) {
            //TODO: MEGA WAZNE!!! freezowanie (a raczej zoomowanie i scrolowanie wykresu)
            //TODO: moze powodowac opoznienia komunikacji z robotem co moze byc calkiem niebezpieczne,
            //TODO: przykladowo opozniajac odbieranie ramek i tym samym spradzanie limitow robota i cyk robot za 20k rozwalony <3
            //TODO: moze po kliknieciu w freeza zrobic diskonekta z robotami?

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
            } else {
                positionChart.UnblockZoomAndPan();
                positionErrorChart.UnblockZoomAndPan();
                velocityChart.UnblockZoomAndPan();
                accelerationChart.UnblockZoomAndPan();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
                fitToDataBtn.IsEnabled = true;
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

        private void LoadConfig(object sender, RoutedEventArgs e) {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog {
                InitialDirectory = Directory.GetCurrentDirectory(),
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

            if ((bool)openFileDialog.ShowDialog() == true) {
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

                            transformationTextBoxes[0, 0].Text = config.Transformation[0, 0].ToString();
                            transformationTextBoxes[0, 1].Text = config.Transformation[0, 1].ToString();
                            transformationTextBoxes[0, 2].Text = config.Transformation[0, 2].ToString();
                            transformationTextBoxes[0, 3].Text = config.Transformation[0, 3].ToString();
                            transformationTextBoxes[1, 0].Text = config.Transformation[1, 0].ToString();
                            transformationTextBoxes[1, 1].Text = config.Transformation[1, 1].ToString();
                            transformationTextBoxes[1, 2].Text = config.Transformation[1, 2].ToString();
                            transformationTextBoxes[1, 3].Text = config.Transformation[1, 3].ToString();
                            transformationTextBoxes[2, 0].Text = config.Transformation[2, 0].ToString();
                            transformationTextBoxes[2, 1].Text = config.Transformation[2, 1].ToString();
                            transformationTextBoxes[2, 2].Text = config.Transformation[2, 2].ToString();
                            transformationTextBoxes[2, 3].Text = config.Transformation[2, 3].ToString();
                            transformationTextBoxes[3, 0].Text = config.Transformation[3, 0].ToString();
                            transformationTextBoxes[3, 1].Text = config.Transformation[3, 1].ToString();
                            transformationTextBoxes[3, 2].Text = config.Transformation[3, 2].ToString();
                            transformationTextBoxes[3, 3].Text = config.Transformation[3, 3].ToString();
                        }
                    }
                } catch (Exception ex) {
                    MainWindow.ShowErrorDialog("Could not load configuration file.", ex);
                }
            }
        }

        private RobotConfig CreateConfiguration() {
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
