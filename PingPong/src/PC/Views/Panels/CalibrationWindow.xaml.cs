using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.Maths;
using PingPong.OptiTrack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace PingPong {
    public partial class CalibrationWindow : Window {

        private class CalibrationTool {

            private readonly Robot robot;

            private readonly OptiTrackSystem optiTrack;

            private readonly BackgroundWorker worker;

            private readonly List<Vector<double>> optiTrackPoints;

            private readonly List<Vector<double>> kukaRobotPoints;

            private readonly List<Vector<double>> calibrationPoints;

            private int samplesPerPoint;

            public event Action Start;

            public event Action<int, Transformation> ProgressChanged;

            public event Action Completed;

            public CalibrationTool(Robot robot, OptiTrackSystem optiTrack) {
                this.robot = robot;
                this.optiTrack = optiTrack;

                worker = new BackgroundWorker() {
                    WorkerSupportsCancellation = true
                };

                optiTrackPoints = new List<Vector<double>>();
                kukaRobotPoints = new List<Vector<double>>();
                calibrationPoints = new List<Vector<double>>();

                worker.DoWork += (s, e) => {
                    Start?.Invoke();

                    // Move robot to first calibration point and wait
                    MoveRobotToCalibrationPoint(calibrationPoints[0], 100);
                    //robot.ForceMoveTo(new RobotVector(calibrationPoints[0], robot.Position.ABC), RobotVector.Zero, 10.0);

                    for (int i = 0; i < calibrationPoints.Count; i++) {
                        if (worker.CancellationPending) {
                            break;
                        }

                        // Move robot to the next calibration point and wait
                        MoveRobotToCalibrationPoint(calibrationPoints[i], 150);
                        //robot.ForceMoveTo(new RobotVector(calibrationPoints[i], robot.Position.ABC), RobotVector.Zero, 4.0);

                        // Add robot XYZ position to list
                        var kukaPoint = robot.Position.XYZ;
                        kukaRobotPoints.Add(kukaPoint);

                        // Gen n samples from optitrack system and add average ball position to the list
                        var optiTrackFrames = optiTrack.WaitForFrames(samplesPerPoint);
                        var optiTrackPoint = Vector<double>.Build.Dense(3);

                        optiTrackFrames.ForEach(frame => {
                            optiTrackPoint += frame.BallPosition;
                        });

                        optiTrackPoints.Add(optiTrackPoint / optiTrackFrames.Count);

                        // Calculate new transformation
                        int progress = i * 100 / (calibrationPoints.Count - 1);
                        var transformation = new Transformation(optiTrackPoints, kukaRobotPoints);

                        ProgressChanged?.Invoke(progress, transformation);
                    }
                };

                worker.RunWorkerCompleted += (s, e) => {
                    if (e.Error != null) {
                        throw e.Error;
                    } else {
                        Completed?.Invoke();
                    }
                };
            }

            private void MoveRobotToCalibrationPoint(Vector<double> point, double velocity) {
                velocity = Math.Min(velocity, 200);

                // Find greatest XYZ displacement
                double deltaX = Math.Abs(point[0] - robot.Position.X);
                double deltaY = Math.Abs(point[1] - robot.Position.Y);
                double deltaZ = Math.Abs(point[2] - robot.Position.Z);
                double deltaMax = Math.Max(Math.Max(deltaX, deltaY), deltaZ);

                // v(T/2)=Vmax => T=15*(x1-x0)/(8*Vmax)
                double duration = Math.Max(15.0 * deltaMax / (8.0 * Math.Abs(velocity)), 1.0);

                robot.ForceMoveTo(new RobotVector(point, robot.Position.ABC), RobotVector.Zero, duration);
            }

            public void Calibrate(int pointsPerLine, int samplesPerPoint) {
                if (worker.IsBusy) {
                    return;
                }

                if (!optiTrack.IsInitialized()) {
                    throw new InvalidOperationException("OptiTrack system is not initialized");
                }

                if (!robot.IsInitialized()) {
                    throw new InvalidOperationException("KUKA robot is not initialized");
                }

                this.samplesPerPoint = samplesPerPoint;
                CalculateCalibrationPoints(pointsPerLine);

                worker.RunWorkerAsync();
            }

            public void Cancel() {
                worker.CancelAsync();
                optiTrackPoints.Clear();
                kukaRobotPoints.Clear();
                calibrationPoints.Clear();
            }

            private void CalculateCalibrationPoints(int pointsPerLine) {
                calibrationPoints.Clear();

                (double x0, double y0, double z0) = robot.Limits.LowerWorkspacePoint;
                (double x1, double y1, double z1) = robot.Limits.UpperWorkspacePoint;

                // Shrink workspace by 5mm
                x0 += x1 > x0 ? 5.0 : -5.0;
                x1 -= x1 > x0 ? 5.0 : -5.0;
                y0 += y1 > y0 ? 5.0 : -5.0;
                y1 -= y1 > y0 ? 5.0 : -5.0;
                z0 += z1 > z0 ? 5.0 : -5.0;
                z1 -= z1 > z0 ? 5.0 : -5.0;

                // Workspace vertices
                (double x, double y, double z) p0 = (x0, y0, z0);
                (double x, double y, double z) p1 = (x1, y0, z0);
                (double x, double y, double z) p2 = (x0, y1, z0);
                (double x, double y, double z) p3 = (x1, y1, z0);
                (double x, double y, double z) p4 = (x0, y0, z1);
                (double x, double y, double z) p5 = (x1, y0, z1);
                (double x, double y, double z) p6 = (x0, y1, z1);
                (double x, double y, double z) p7 = (x1, y1, z1);

                var points = new[] { p0, p5, p3, p6, p0, p4, p1, p7, p2, p4 };

                for (int i = 0; i < points.Length - 1; i++) {
                    (double px0, double py0, double pz0) = points[i];
                    (double px1, double py1, double pz1) = points[i + 1];

                    var deltaX = (px1 - px0) / (pointsPerLine + 1);
                    var deltaY = (py1 - py0) / (pointsPerLine + 1);
                    var deltaZ = (pz1 - pz0) / (pointsPerLine + 1);

                    for (int j = 0; j < pointsPerLine + 1; j++) {
                        calibrationPoints.Add(Vector<double>.Build.DenseOfArray(new double[] {
                            px0 + deltaX * j,
                            py0 + deltaY * j,
                            pz0 + deltaZ * j
                        }));
                    }
                }
            }

        }

        private readonly CalibrationTool calibrationTool;

        public Action<Transformation> Completed;

        public CalibrationWindow(Robot robot, OptiTrackSystem optiTrack) {
            InitializeComponent();

            Transformation currentTransformation = null;
            calibrationTool = new CalibrationTool(robot, optiTrack);

            calibrationTool.Start += () => {
                Dispatcher.Invoke(() => {
                    progressBar.Value = 0;
                    progressBarLabel.Content = "0%";
                    startBtn.IsEnabled = false;
                    cancelBtn.IsEnabled = true;
                });
            };

            calibrationTool.ProgressChanged += (progress, transformation) => {
                currentTransformation = transformation;

                Dispatcher.Invoke(() => {
                    progressBar.Value = progress;
                    progressBarLabel.Content = $"{progress}%";

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

            calibrationTool.Completed += () => {
                Completed?.Invoke(currentTransformation);
                Close();
            };

            startBtn.Click += (s, e) => {
                try {
                    calibrationTool.Calibrate(10, 200);
                } catch (Exception ex) {
                    MainWindow.ShowErrorDialog("Unable to start calibration.", ex);
                }
            };
            cancelBtn.Click += (s, e) => {
                calibrationTool.Cancel();
            };

            Closing += (s, e) => {
                calibrationTool.Cancel();
            };
            robotIpAdress.Content += robot.ToString();
        }

    }
}
