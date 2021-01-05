using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PingPong {
    public partial class RobotPanel : UserControl {

        private bool isPlotFrozen = false;

        public RobotPanel() {
            InitializeComponent();
            InitializePositionChart();
            InitializeVelocityChart();
            InitializeAccelerationChart();
            InitializePositionErrorChart();

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
        }

        private void InitializePositionChart() {
            positionChart.YAxisTitle = "Position (actual)";
            positionChart.AddSeries("Position X [mm]", "X", true);
            positionChart.AddSeries("Position Y [mm]", "Y", true);
            positionChart.AddSeries("Position Z [mm]", "Z", true);
            positionChart.AddSeries("Position A [deg]", "A", false);
            positionChart.AddSeries("Position B [deg]", "B", false);
            positionChart.AddSeries("Position C [deg]", "C", false);
        }

        private void InitializePositionErrorChart() {
            positionErrorChart.YAxisTitle = "Position error";
            positionErrorChart.AddSeries("Error X [mm]", "X", true);
            positionErrorChart.AddSeries("Error Y [mm]", "Y", true);
            positionErrorChart.AddSeries("Error Z [mm]", "Z", true);
            positionErrorChart.AddSeries("Error A [deg]", "A", false);
            positionErrorChart.AddSeries("Error B [deg]", "B", false);
            positionErrorChart.AddSeries("Error C [deg]", "C", false);
        }

        private void InitializeVelocityChart() {
            velocityChart.YAxisTitle = "Velocity (theoretical)";
            velocityChart.AddSeries("Velocity X [mm/s]", "X", true);
            velocityChart.AddSeries("Velocity Y [mm/s]", "Y", true);
            velocityChart.AddSeries("Velocity Z [mm/s]", "Z", true);
            velocityChart.AddSeries("Velocity A [deg/s]", "A", false);
            velocityChart.AddSeries("Velocity B [deg/s]", "B", false);
            velocityChart.AddSeries("Velocity C [deg/s]", "C", false);
        }

        private void InitializeAccelerationChart() {
            accelerationChart.YAxisTitle = "Acceleration (theoretical)";
            accelerationChart.AddSeries("Acceleration X [mm/s]", "X", true);
            accelerationChart.AddSeries("Acceleration Y [mm/s]", "Y", true);
            accelerationChart.AddSeries("Acceleration Z [mm/s]", "Z", true);
            accelerationChart.AddSeries("Acceleration A [deg/s]", "A", false);
            accelerationChart.AddSeries("Acceleration B [deg/s]", "B", false);
            accelerationChart.AddSeries("Acceleration C [deg/s]", "C", false);
        }

    }
}
