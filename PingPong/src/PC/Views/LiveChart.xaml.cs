﻿using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace PingPong {
    public partial class LiveChart : UserControl {

        private readonly Stopwatch stopWatch = new Stopwatch();

        private long sample = 0;

        private long deltaTime = 0;

        public int RefreshDelay { get; set; } = 100;

        public int MaxSamples { get; set; } = 5000;

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
                throw new ArgumentException();
            }

            stopWatch.Stop();

            deltaTime += stopWatch.ElapsedMilliseconds;

            stopWatch.Reset();
            stopWatch.Start();

            if (deltaTime < RefreshDelay) {
                sample++;
                return;
            } else {
                deltaTime = 0;
            }

            for (int i = 0; i < data.Length; i++) {
                ((List<DataPoint>)chart.Series[i].ItemsSource).Add(new DataPoint(sample, data[i]));
            }

            Dispatcher.Invoke(() => chart.InvalidatePlot(true));

            sample++;
        }

        public void Clear() {
            foreach (var series in chart.Series) {
                ((List<DataPoint>)series.ItemsSource).Clear();
            }
        }

        public void Freeze() {

        }

        public void Unfreeze() {

        }

        public void ResetZoom() {

        }

    }
}
