using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PingPong {
    public partial class RobotPanel : UserControl {

        private bool isPlotFrozen = false;

        private ManualModeWindow manualModeWindow;

        private CalibrationWindow calibrationWindow;

        private LiveChart activeChart;

        public KUKARobot Robot { get; } = new KUKARobot();

        public MainWindow MainWindowHandle { get; set; }

        public RobotPanel() {
            InitializeComponent();
            InitializeControls();
            InitializeCharts();
            InitializeRobot();

            RobotVector actualPosition = new RobotVector();
            RobotVector targetPosition = new RobotVector(100, 0, 0);
            TrajectoryGenerator5v1 generator = new TrajectoryGenerator5v1();

            generator.Restart(actualPosition);
            generator.SetTargetPosition(targetPosition, new RobotVector(50, 0, 0), 10);

            Random rand = new Random();

            Task.Run(() => {
                for (int i = 0; i < 15000; i++) {
                    actualPosition = generator.GetNextAbsoluteCorrection(actualPosition);

                    Thread.Sleep(4);

                    if (isPlotFrozen) {
                        continue;
                    }

                    positionChart.Update(actualPosition.ToArray());
                    positionErrorChart.Update(generator.PositionError.ToArray());
                    velocityChart.Update(generator.Velocity.ToArray());
                    accelerationChart.Update(generator.Acceleration.ToArray());
                }
            });
        }

        private void InitializeControls() {
            connectBtn.Click += Connect;
            freezeBtn.Click += FreezeOrUnfreeze;
            loadConfigBtn.Click += LoadConfig;
            disconnectBtn.Click += Disconnect;

            resetZoomBtn.Click += (s, e) => {
                positionChart.ResetZoom();
                positionErrorChart.ResetZoom();
                velocityChart.ResetZoom();
                accelerationChart.ResetZoom();
            };
            saveConfigBtn.Click += (s, e) => {
                CreateConfiguration().SaveToFile();
            };
            manualModeBtn.Click += (s, e) => {
                if (manualModeWindow == null) {
                    manualModeWindow = new ManualModeWindow(Robot) {
                        Owner = MainWindowHandle
                    };

                    manualModeWindow.Closed += (se, ev) => manualModeWindow = null;
                    manualModeWindow.Show();
                } else {
                    manualModeWindow.Activate();
                }
            };
            calibrateBtn.Click += (s, e) => {
                if (calibrationWindow == null) {
                    calibrationWindow = new CalibrationWindow(Robot, null) { //TODO: DODAC OPTITRACKA
                        Owner = MainWindowHandle
                    };

                    calibrationWindow.Closed += (se, ev) => calibrationWindow = null;
                    calibrationWindow.Show();
                } else {
                    calibrationWindow.Activate();
                }
            };

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
                RobotVector positionError = Robot.PositionError;
                RobotVector velocity = Robot.Velocity;
                RobotVector acceleration = Robot.Acceleration;
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

                //TODO: mozna by zoptymalizowac zeby nie tworzyc bez sensu tych tablic, bo wykres i tak sie nie zupdatuje
                positionChart.Update(new double[] {
                    position.X, position.Y, position.Z, position.A, position.B, position.C
                });

                positionErrorChart.Update(new double[] {
                    positionError.X, positionError.Y, positionError.Z, positionError.A, positionError.B, positionError.C
                });

                velocityChart.Update(new double[] {
                    velocity.X, velocity.Y, velocity.Z, velocity.A, velocity.B, velocity.C
                });

                accelerationChart.Update(new double[] {
                    acceleration.X, acceleration.Y, acceleration.Z, acceleration.A, acceleration.B, acceleration.C
                });
            };
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

                positionChart.BlockZoomingAndPanning();
                positionErrorChart.BlockZoomingAndPanning();
                velocityChart.BlockZoomingAndPanning();
                accelerationChart.BlockZoomingAndPanning();

                isPlotFrozen = false;
                freezeBtn.Content = "Freeze";
                resetZoomBtn.IsEnabled = false;
            } else {
                positionChart.UnblockZoomingAndPanning();
                positionErrorChart.UnblockZoomingAndPanning();
                velocityChart.UnblockZoomingAndPanning();
                accelerationChart.UnblockZoomingAndPanning();

                isPlotFrozen = true;
                freezeBtn.Content = "Unfreeze";
                resetZoomBtn.IsEnabled = true;
            }
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

                            t00.Text = config.Transformation[0, 0].ToString();
                            t01.Text = config.Transformation[0, 1].ToString();
                            t02.Text = config.Transformation[0, 2].ToString();
                            t03.Text = config.Transformation[0, 3].ToString();

                            t10.Text = config.Transformation[1, 0].ToString();
                            t11.Text = config.Transformation[1, 1].ToString();
                            t12.Text = config.Transformation[1, 2].ToString();
                            t13.Text = config.Transformation[1, 3].ToString();

                            t20.Text = config.Transformation[2, 0].ToString();
                            t21.Text = config.Transformation[2, 1].ToString();
                            t22.Text = config.Transformation[2, 2].ToString();
                            t23.Text = config.Transformation[2, 3].ToString();

                            t30.Text = config.Transformation[3, 0].ToString();
                            t31.Text = config.Transformation[3, 1].ToString();
                            t32.Text = config.Transformation[3, 2].ToString();
                            t33.Text = config.Transformation[3, 3].ToString();
                        }
                    }
                } catch (Exception ex) {
                    MainWindow.ShowErrorDialog("Could not load configuration file.", ex);
                }
            }
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
