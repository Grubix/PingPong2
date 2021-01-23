using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;

namespace PingPong {
    public partial class MainWindow : Window {

        private static MainWindow mainWindow;

        public MainWindow() {
            InitializeComponent();

            // Set timer resolution to 1 [ms]
            WinApi.TimeBeginPeriod(1);

            mainWindow = this;
            // Force change number separator to dot
            CultureInfo culuteInfo = new CultureInfo("en-US");
            culuteInfo.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = culuteInfo;
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            Loaded += (s, e) => {
                robot1Panel.MainWindowHandle = this;
                robot1Panel.OptiTrack = optiTrackPanel.OptiTrack;
                robot1Panel.Robot.ErrorOccured += (sender, args) => {
                    ShowErrorDialog($"An exception was raised on the robot ({args.RobotIp}) thread.", args.Exception);
                };

                robot2Panel.MainWindowHandle = this;
                robot2Panel.OptiTrack = optiTrackPanel.OptiTrack;
                robot2Panel.Robot.ErrorOccured += (sender, args) => {
                    ShowErrorDialog($"An exception was raised on the robot ({args.RobotIp}) thread.", args.Exception);
                };

                try {
                    robot1Panel.LoadConfig("Config/robot1.config.json");
                    robot2Panel.LoadConfig("Config/robot2.config.json");
                } catch (Exception) {
                    // Ingore exception - config files may not exist in Config folder
                }

                optiTrackPanel.Initialize(robot1Panel.Robot, robot2Panel.Robot);
                pingPongPanel.Initialize(robot1Panel.Robot, robot2Panel.Robot, optiTrackPanel.OptiTrack );
            };

            // Shrink window if it is too wide or too high
            double windowWidth = Width;
            double windowHeight = Height;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            if (windowWidth >= screenWidth) {
                Width = MinWidth = screenWidth - 100;
            }

            if (windowHeight >= screenHeight) {
                Height = MinHeight = screenHeight - 100;
            }

            // Robots configuration files directory
            Directory.CreateDirectory("Config");

            // Chart screenshots directory
            Directory.CreateDirectory("Screenshots");
        }

        public static void ShowErrorDialog(string errorMessage, Exception exception = null) {
            if (exception != null) {
                errorMessage += $"\nOriginal error: \"{exception.Message}\"";
            }

            if (mainWindow != null) {
                mainWindow.Dispatcher.Invoke(() => {
                    MessageBox.Show(mainWindow, errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            } else {
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
