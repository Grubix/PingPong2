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

        private readonly Stopwatch stopWatch;

        private readonly List<SeriesWrapper> wrappedSeries;

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

            stopWatch = new Stopwatch();
            wrappedSeries = new List<SeriesWrapper>();

            chart.Axes[0].Minimum = 0;
            chart.Axes[0].Maximum = MaxSamples;
            chart.Axes[0].AbsoluteMinimum = 0;
            chart.Axes[0].AbsoluteMaximum = MaxSamples;
            chart.Axes[0].MinimumRange = 10;
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
                throw new ArgumentException("Data array length error");
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
                
                if (currentSample % 2 == 0) {
                    wrappedSeries[i].Points.Add(point);
                }

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

    }
}
