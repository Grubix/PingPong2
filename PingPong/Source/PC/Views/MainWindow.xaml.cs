using PingPong.KUKA;
using PingPong.OptiTrack;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PingPong {
    public partial class MainWindow : Window {

        private static MainWindow mainWindowHanlde;

        public MainWindow() {
            InitializeComponent();
            mainWindowHanlde = this;

            // Set timer resolution to 1ms (15.6ms is default)
            WinApi.TimeBeginPeriod(1);

            // Force change number separator to dot
            CultureInfo culuteInfo = new CultureInfo("en-US");
            culuteInfo.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = culuteInfo;
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            Loaded += (s, e) => {
                robot1Panel.MainWindowHandle = this;
                robot1Panel.OptiTrack = optiTrackPanel.OptiTrack;
                robot2Panel.MainWindowHandle = this;
                robot2Panel.OptiTrack = optiTrackPanel.OptiTrack;

                try {
                    robot1Panel.LoadConfig(Path.Combine(App.ConfigDir, "robot1.config.json"));
                } catch (Exception) {

                }

                try {
                    robot2Panel.LoadConfig(Path.Combine(App.ConfigDir, "robot2.config.json"));
                } catch (Exception) {

                }

                Robot robot = robot1Panel.Robot;
                Robot robot2 = robot2Panel.Robot;
                OptiTrackSystem optiTrack = optiTrackPanel.OptiTrack;

                optiTrackPanel.Initialize(robot, robot2);
                pingPongPanel.Initialize(robot, robot2, optiTrack);

                robot.ErrorOccured += (sender, args) => {
                    //TODO: czemu to zakomentowane wywala wyjatek (FatalError) ?

                    //robot2.Uninitialize();
                    //optiTrack.Uninitialize();

                    robot1Panel.ForceFreezeCharts();
                    robot2Panel.ForceFreezeCharts();
                    //optiTrackPanel.ForceFreezeCharts();
                    //pingPongPanel.ForceFreezeCharts();

                    ShowErrorDialog($"An exception was raised on the robot ({args.RobotIp}) thread.", args.Exception);
                };

                robot2.ErrorOccured += (sender, args) => {
                    //TODO: czemu to zakomentowane wywala wyjatek (FatalError) ?

                    //robot.Uninitialize();
                    //optiTrack.Uninitialize();

                    robot1Panel.ForceFreezeCharts();
                    robot2Panel.ForceFreezeCharts();
                    //optiTrackPanel.ForceFreezeCharts();
                    //pingPongPanel.ForceFreezeCharts();

                    ShowErrorDialog($"An exception was raised on the robot ({args.RobotIp}) thread.", args.Exception);
                };

                //TODO: ODBICIE LUSTRZANE
                //Robot robot1 = robot1Panel.Robot;
                //Robot robot2 = robot2Panel.Robot;

                //robot1.FrameSent += (sender, args) => {
                //    RobotVector targetPosition = new RobotVector(
                //        args.TargetPosition.Y,
                //        args.TargetPosition.X,
                //        args.TargetPosition.Z,
                //        robot2.HomePosition.ABC
                //    );

                //    robot2.MoveTo(args.TargetPosition, args.TargetVelocity, args.TargetDuration);
                //};
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
            Directory.CreateDirectory(App.ConfigDir);

            // Chart screenshots directory
            Directory.CreateDirectory(App.ScreenshotsDir);
        }

        public static void ShowErrorDialog(string errorMessage, Exception exception = null) {
            if (exception != null) {
                errorMessage += $"\nOriginal error: \"{exception.Message}\"";
            }

            if (mainWindowHanlde != null) {
                mainWindowHanlde.Dispatcher.Invoke(() => {
                    MessageBox.Show(mainWindowHanlde, errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            } else {
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
