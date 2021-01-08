using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace PingPong {
    public partial class LiveChart : UserControl {

        private readonly object updateSyncLock = new object();

        private readonly Stopwatch stopWatch = new Stopwatch();

        private long deltaTime = 0;

        private int currentSample = 0;

        private int clearCounter = 0;

        private int refreshDelay = 80;

        private int maxSamples = 5000;

        public int RefreshDelay {
            get {
                lock (updateSyncLock) {
                    return refreshDelay;
                }
            }
            set {
                lock (updateSyncLock) {
                    refreshDelay = value;
                }
            }
        }

        public int MaxSamples {
            get {
                lock (updateSyncLock) {
                    return maxSamples;
                }
            }
            set {
                lock (updateSyncLock) {
                    maxSamples = value;
                }
                Clear();
            }
        }

        public string YAxisTitle {
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
        }

        public void AddSeries(string title, string name, bool visible) {
            LineSeries series = new LineSeries {
                Title = title
            };

            chart.Series.Add(series);
            series.ItemsSource = new List<DataPoint>();

            CheckBox checkbox = new CheckBox {
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            if (visible) {
                checkbox.IsChecked = true;
            } else {
                series.Visibility = Visibility.Hidden;
            }

            if (visibleSeries.Children.Count != 0) {
                checkbox.Margin = new Thickness(5, 0, 0, 0);
            }

            checkbox.Click += (s, e) => {
                if ((bool)checkbox.IsChecked) {
                    series.Visibility = Visibility.Visible;
                } else {
                    series.Visibility = Visibility.Hidden;
                }
            };

            Label label = new Label {
                Content = name
            };

            StackPanel panel = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            panel.Children.Add(checkbox);
            panel.Children.Add(label);
            visibleSeries.Children.Add(panel);
        }

        public void Update(double[] data) {
            if (data.Length != chart.Series.Count) {
                throw new ArgumentException("Array length err");
            }

            stopWatch.Stop();

            deltaTime += stopWatch.ElapsedMilliseconds;

            stopWatch.Reset();
            stopWatch.Start();

            if (deltaTime >= RefreshDelay) {
                deltaTime = 0;

                lock (updateSyncLock) {
                    for (int i = 0; i < data.Length; i++) {
                        ((List<DataPoint>)chart.Series[i].ItemsSource).Add(new DataPoint(currentSample, data[i]));
                    }
                }

                Dispatcher.Invoke(() => {
                    lock (updateSyncLock) {
                        if (currentSample > (clearCounter + 1) * maxSamples) {
                            clearCounter++;

                            chart.Axes[0].Minimum = chart.Axes[0].AbsoluteMinimum = clearCounter * maxSamples;
                            chart.Axes[0].Maximum = (clearCounter + 1) * maxSamples;

                            for (int i = 0; i < data.Length; i++) {
                                ((List<DataPoint>)chart.Series[i].ItemsSource).Clear();
                            }
                        }

                        chart.InvalidatePlot();
                    }
                });
            }

            lock (updateSyncLock) {
                currentSample++;
            }
        }

        public void Clear() {
            lock (updateSyncLock) {
                foreach (var series in chart.Series) {
                    ((List<DataPoint>)series.ItemsSource).Clear();
                }

                currentSample = 0;
                clearCounter = 0;

                chart.Axes[0].Minimum = 0;
                chart.Axes[0].AbsoluteMinimum = 0;
                chart.Axes[0].Maximum = maxSamples;
                chart.ResetAllAxes();

                chart.InvalidatePlot();
            }
        }

        public void BlockZoomingAndPanning() {
            chart.Axes[0].IsZoomEnabled = false;
            chart.Axes[0].IsPanEnabled = false;
            chart.InvalidatePlot();
        }

        public void UnblockZoomingAndPanning() {
            chart.Axes[0].IsZoomEnabled = true;
            chart.Axes[0].IsPanEnabled = true;
            chart.InvalidatePlot();
        }

        public void ResetZoom() {
            chart.ResetAllAxes();
            chart.InvalidatePlot();
        }

    }
}
