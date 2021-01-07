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

            Closing += (s, e) => {
                //TODO: disconect robotów i optitracka przed zamknieciem okna
            };
        }

    }
}
