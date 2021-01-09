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

            robot1Panel.MainWindowHandle = this;
            robot2Panel.MainWindowHandle = this;
        }

        public static void ShowErrorDialog(string errorMessage, Exception exception = null) {
            if (exception != null) {
                errorMessage += $"\nOriginal error: \"{exception.Message}\"";
            }

            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

    }
}
