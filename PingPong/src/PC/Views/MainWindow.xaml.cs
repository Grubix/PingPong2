using System;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace PingPong {
    public partial class MainWindow : Window {

        public MainWindow() {
            InitializeComponent();

            CultureInfo culuteInfo = new CultureInfo("en-US");
            culuteInfo.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = culuteInfo;
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            Loaded += (s, e) => {
                robot1Panel.MainWindowHandle = this;
                robot1Panel.OptiTrack = optiTrackPanel.OptiTrack;

                robot2Panel.MainWindowHandle = this;
                robot2Panel.OptiTrack = optiTrackPanel.OptiTrack;

                optiTrackPanel.Robot1 = robot1Panel.Robot;
                optiTrackPanel.Robot2 = robot2Panel.Robot;
            };
        }

        public static void ShowErrorDialog(string errorMessage, Exception exception = null) {
            if (exception != null) {
                errorMessage += $"\nOriginal error: \"{exception.Message}\"";
            }

            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

    }
}
