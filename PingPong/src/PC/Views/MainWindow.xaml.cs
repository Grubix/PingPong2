using PingPong.KUKA;
using PingPong.OptiTrack;
using System.Windows;

namespace PingPong
{
    public partial class MainWindow : Window {

        private KUKARobot robot1;

        private KUKARobot robot2;

        private OptiTrackSystem optiTrack;

        public MainWindow() {
            InitializeComponent();
        }

    }
}
