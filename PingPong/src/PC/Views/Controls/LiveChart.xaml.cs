using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PingPong {
    public partial class LiveChart : UserControl {

        private class SeriesWrapper {

            public List<DataPoint> Points { get; }

            public List<DataPoint> DelayedPoints { get; }

            public Series Series { get; }

            public SeriesWrapper(string title) {
                Series = new LineSeries {
                    Title = title
                };

                Points = new List<DataPoint>();
                DelayedPoints = new List<DataPoint>();

                Series.ItemsSource = DelayedPoints;
            }

            public void Clear() {
                Points.Clear();
                DelayedPoints.Clear();
            }

        }

        private readonly Stopwatch stopWatch = new Stopwatch();

        private readonly List<SeriesWrapper> wrappedSeries  = new List<SeriesWrapper>();

        private long deltaTime = 0;

        private int currentSample = 0;

        private int lastUpdateSample = 0;

        private int clearCounter = 0;

        public int RefreshDelay { get; set; } = 180;

        public int MaxSamples { get; set; } = 5000;

        public bool IsFrozen { get; private set; }

        public string Title {
            get {
                return chart.Axes[1].Title;
            }
            set {
                chart.Axes[1].Title = value;
            }
        }

        public LiveChart() {
            InitializeComponent();
            chart.Axes[0].Minimum = 0;
            chart.Axes[0].Maximum = MaxSamples;
            chart.Axes[0].AbsoluteMinimum = 0;
            chart.Axes[0].AbsoluteMaximum = MaxSamples;
            chart.Axes[0].MinimumRange = 10;

            //chart.Axes[0].Title = "Ball position X [mm]";

            //Series series1 = new LineSeries {
            //    Title = "Beta = 1.5"
            //};

            //var points1 = new List<DataPoint>();
            //series1.ItemsSource = points1;

            //Series series2 = new LineSeries {
            //    Title = "Beta = 1.5"
            //};

            //var points2 = new List<DataPoint>();
            //series2.ItemsSource = points2;

            //Series series3 = new LineSeries {
            //    Title = "Beta = 0.5"
            //};

            //var points3 = new List<DataPoint>();
            //series3.ItemsSource = points3;

            //Series series4 = new LineSeries {
            //    Title = "Beta = 0.01"
            //};

            //var points4 = new List<DataPoint>();
            //series4.ItemsSource = points4;

            //Series series5 = new LineSeries {
            //    Title = "z = z0 + vz0 * t - g / 2 * t^2"
            //};

            //var points5 = new List<DataPoint>();
            //series5.ItemsSource = points5;

            //chart.Series.Add(series1);
            ////chart.Series.Add(series2);
            //chart.Series.Add(series3);
            //chart.Series.Add(series4);
            //chart.Series.Add(series5);

            //double beta1 = 1.5;
            //double beta2 = 1.5;
            //double beta3 = 0.5;
            //double beta4 = 0.01;

            //double x0 = 0;
            //double vx0 = 3;

            //double z0 = 0;
            //double vz0 = 6;

            //double g = 9.81;

            //for (double t = 0; t < 2; t += 0.004) {
            //    double x = x0 + vx0 * t;
            //    double x1 = x0 + vx0 / beta1 * (1 - Math.Exp(-beta1 * t));
            //    double x2 = x0 + vx0 / beta2 * (1 - Math.Exp(-beta2 * t));
            //    double x3 = x0 + vx0 / beta3 * (1 - Math.Exp(-beta3 * t));
            //    double x4 = x0 + vx0 / beta4 * (1 - Math.Exp(-beta4 * t));

            //    double z = z0 + vz0 * t - g / 2 * t * t;
            //    double z1 = z0 + (beta1 * vz0 + g) / (beta1 * beta1) * (1 - Math.Exp(-beta1 * t)) - g / beta1 * t;
            //    double z2 = z0 + (beta2 * vz0 + g) / (beta2 * beta2) * (1 - Math.Exp(-beta2 * t)) - g / beta2 * t;
            //    double z3 = z0 + (beta3 * vz0 + g) / (beta3 * beta3) * (1 - Math.Exp(-beta3 * t)) - g / beta3 * t;
            //    double z4 = z0 + (beta4 * vz0 + g) / (beta4 * beta4) * (1 - Math.Exp(-beta4 * t)) - g / beta4 * t;

            //    if (z1 >= 0) {
            //        points1.Add(new DataPoint(x1 * 1000, z1 * 1000));
            //    }
            //    if (z2 >= 0) {
            //        points2.Add(new DataPoint(x2 * 1000, z2 * 1000));
            //    }
            //    if (z3 >= 0) {
            //        points3.Add(new DataPoint(x3 * 1000, z3 * 1000));
            //    }
            //    if (z4 >= 0) {
            //        points4.Add(new DataPoint(x4 * 1000, z4 * 1000));
            //    }

            //    if (z >= 0) {
            //        points5.Add(new DataPoint(x * 1000, z * 1000));
            //    }
            //}

            //chart.InvalidatePlot();
        }

        public void AddSeries(string title, string text, bool visible, bool addSeparator = false) {
            SeriesWrapper seriesWrapper = new SeriesWrapper(title);
            chart.Series.Add(seriesWrapper.Series);
            wrappedSeries.Add(seriesWrapper);

            CheckBox checkbox = new CheckBox {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0),
            };

            if (visible) {
                checkbox.IsChecked = true;
            } else {
                seriesWrapper.Series.Visibility = Visibility.Hidden;
            }

            if (visibleSeries.Children.Count != 0) {
                checkbox.Margin = new Thickness(5, 0, 0, 0);
            }

            checkbox.Click += (s, e) => {
                if ((bool)checkbox.IsChecked) {
                    seriesWrapper.Series.Visibility = Visibility.Visible;
                } else {
                    seriesWrapper.Series.Visibility = Visibility.Hidden;
                }
            };

            StackPanel checkboxLabel = new StackPanel {
                Orientation = Orientation.Horizontal,
            };

            string[] textSplit = text.Split('_');

            TextBlock label = new TextBlock {
                Text = textSplit[0]
            };

            checkboxLabel.Children.Add(label);

            if (textSplit.Length > 1) {
                TextBlock labelSubscript = new TextBlock {
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Margin = new Thickness(1, 0, 0, -3),
                    FontSize = 10,
                    Text = textSplit[1]
                };

                checkboxLabel.Children.Add(labelSubscript);
            }

            checkbox.Content = checkboxLabel;

            visibleSeries.Children.Add(checkbox);

            if (addSeparator) {
                Rectangle separator1 = new Rectangle {
                    Width = 1.0,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    SnapsToDevicePixels = true,
                    Margin = new Thickness(8, 0, 0, 0),
                };

                Rectangle separator2 = new Rectangle {
                    Width = 1.0,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    SnapsToDevicePixels = true,
                    Margin = new Thickness(2, 0, 8, 0)
                };

                SolidColorBrush brush = new SolidColorBrush {
                    Color = Color.FromArgb(255, 160, 160, 160)
                };

                separator1.Stroke = brush;
                separator2.Stroke = brush;

                visibleSeries.Children.Add(separator1);
                visibleSeries.Children.Add(separator2);
            }
        }

        public void Update(double[] data) {
            if (IsFrozen) {
                return;
            }

            if (data.Length != chart.Series.Count) {
                throw new ArgumentException("Array length err");
            }

            stopWatch.Stop();
            deltaTime += stopWatch.ElapsedMilliseconds;
            stopWatch.Restart();

            bool isReady = deltaTime >= RefreshDelay;

            if (isReady) {
                deltaTime = 0;
            }

            for (int i = 0; i < data.Length; i++) {
                DataPoint point = new DataPoint(currentSample, data[i]);
                wrappedSeries[i].Points.Add(point);

                if (isReady) {
                    wrappedSeries[i].DelayedPoints.Add(point);
                }
            }

            Dispatcher.Invoke(() => {
                if (currentSample > (clearCounter + 1) * MaxSamples) {
                    clearCounter++;

                    chart.Axes[0].Minimum = chart.Axes[0].AbsoluteMinimum = clearCounter * MaxSamples;
                    chart.Axes[0].Maximum = chart.Axes[0].AbsoluteMaximum = (clearCounter + 1) * MaxSamples;

                    wrappedSeries.ForEach(s => s.Clear());
                }

                lastUpdateSample = currentSample;

                if (isReady) {
                    chart.InvalidatePlot();
                }
            });

            currentSample++;
        }

        public void Clear() {
            wrappedSeries.ForEach(s => s.Clear());

            currentSample = 0;
            lastUpdateSample = 0;
            clearCounter = 0;

            chart.Axes[0].Minimum = 0;
            chart.Axes[0].Maximum = MaxSamples;
            chart.Axes[0].AbsoluteMinimum = 0;
            chart.Axes[0].AbsoluteMaximum = MaxSamples;
            chart.ResetAllAxes();
            chart.InvalidatePlot();
        }

        public void ResetZoom() {
            chart.Axes[0].Minimum = chart.Axes[0].AbsoluteMinimum = clearCounter * MaxSamples;
            chart.Axes[0].Maximum = chart.Axes[0].AbsoluteMaximum = (clearCounter + 1) * MaxSamples;
            chart.ResetAllAxes();
            chart.InvalidatePlot();
        }

        public void FitToData() {
            chart.Axes[0].Maximum = lastUpdateSample;
            chart.ResetAllAxes();
            chart.InvalidatePlot();
        }

        public void Freeze() {
            IsFrozen = true;

            wrappedSeries.ForEach(s => s.Series.ItemsSource = s.Points);

            chart.Axes[0].IsZoomEnabled = true;
            chart.Axes[0].IsPanEnabled = true;
            chart.InvalidatePlot();
        }

        public void Unfreeze() {
            IsFrozen = false;

            wrappedSeries.ForEach(s => s.Series.ItemsSource = s.DelayedPoints);

            chart.Axes[0].IsZoomEnabled = false;
            chart.Axes[0].IsPanEnabled = false;
            Clear(); 
        }

        public MemoryStream ExportPng(int width, int height) {
            MemoryStream imageStream = new MemoryStream();

            var pngExporter = new PngExporter {
                Width = width,
                Height = height,
                Background = OxyColors.White
            };

            pngExporter.Export(chart.ActualModel, imageStream);

            return imageStream;
        }

        public MemoryStream ExportSvg(int width, int height) {
            MemoryStream imageStream = new MemoryStream();

            var pngExporter = new OxyPlot.SvgExporter {
                Width = width,
                Height = height
            };

            pngExporter.Export(chart.ActualModel, imageStream);

            return imageStream;
        }

    }
}
