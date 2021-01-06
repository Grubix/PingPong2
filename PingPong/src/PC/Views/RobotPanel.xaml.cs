﻿using PingPong.KUKA;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PingPong {
    public partial class RobotPanel : UserControl {

        private bool isPlotFrozen = false;

        private KUKARobot robot;

        public RobotPanel() {
            InitializeComponent();
            InitializeCharts();

            Task.Run(() => {
                for (int i = 0; i < 20000; i++) {
                    Thread.Sleep(4);

                    if (isPlotFrozen) {
                        continue;
                    }

                    positionChart.Update(new double[] {
                        Math.Sin(i / 250.0) / 6.0,
                        Math.Cos(i / 250.0) / 5.0,
                        Math.Sin(i / 500.0) / 4.0,
                        Math.Cos(i / 500.0) / 3.0,
                        Math.Sin(i / 750.0) / 2.0,
                        Math.Cos(i / 750.0) / 1.0
                    });
                }
            });

            //TODO: MEGA WAZNE, moze freezowanie powinno byz dostepne tylko dla robota disconected ?? scrolowanie i zoomowanie mega obciaza procka
            //TODO: alternatywnie klikniecie na freeza moze powodowac disconecta
            freezeBtn.Click += (s, e) => {
                if (isPlotFrozen) {
                    positionChart.BlockZoomingAndPanning();
                    positionErrorChart.BlockZoomingAndPanning();
                    velocityChart.BlockZoomingAndPanning();
                    accelerationChart.BlockZoomingAndPanning();

                    positionChart.Clear();
                    positionErrorChart.Clear();
                    velocityChart.Clear();
                    accelerationChart.Clear();

                    positionChart.ResetZoom();
                    positionErrorChart.ResetZoom();
                    velocityChart.ResetZoom();
                    accelerationChart.ResetZoom();

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
            };

            resetZoomBtn.Click += (s, e) => {
                positionChart.ResetZoom();
                positionErrorChart.ResetZoom();
                velocityChart.ResetZoom();
                accelerationChart.ResetZoom();
            };

            loadConfigBtn.Click += LoadConfig;
        }

        private void LoadConfig(object sender, RoutedEventArgs e) {
            var fileDialog = new Microsoft.Win32.OpenFileDialog {
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


            if ((bool)fileDialog.ShowDialog() == true) {
                Stream fileStream;
                StreamReader streamReader;

                try {
                    if ((fileStream = fileDialog.OpenFile()) != null) {
                        using (fileStream)
                        using (streamReader = new StreamReader(fileStream)) {
                            string jsonString = streamReader.ReadToEnd();
                            RobotConfig config = new RobotConfig(jsonString);

                            port.Text = config.Port.ToString();

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
                    MessageBox.Show($"Could not load configuration file. Original error: \"{ex.Message}\"",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
            accelerationChart.AddSeries("Acceleration X [mm/s]", "X", true);
            accelerationChart.AddSeries("Acceleration Y [mm/s]", "Y", true);
            accelerationChart.AddSeries("Acceleration Z [mm/s]", "Z", true);
            accelerationChart.AddSeries("Acceleration A [deg/s]", "A", false);
            accelerationChart.AddSeries("Acceleration B [deg/s]", "B", false);
            accelerationChart.AddSeries("Acceleration C [deg/s]", "C", false);
        }

        public void Connect() {
            //TODO: tworzenie / updatowanie instacji w zależności od konfiguracji
        }

        public void SetRobot(KUKARobot robot) {
            this.robot = robot;

            //TODO: ip, limits;

            //TODO: on initialize -> ip, port

            robot.Initialized += () => {
                connectBtn.Content = "Disconnect";
                calibrateBtn.IsEnabled = false;
                loadConfigBtn.IsEnabled = false;
                saveConfigBtn.IsEnabled = false;
            };

            robot.FrameReceived += frame => {
                RobotVector position = robot.Position;
                RobotVector positionError = robot.PositionError;
                RobotVector velocity = robot.Velocity;
                RobotVector acceleration = robot.Acceleration;
                RobotVector targetPosition = robot.TargetPosition;

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

        private RobotConfig CreateConfiguration() {
            return null;
        }

    }
}
