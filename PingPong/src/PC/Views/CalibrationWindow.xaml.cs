using PingPong.KUKA;
using PingPong.OptiTrack;
using System.Windows;

namespace PingPong {
    public partial class CalibrationWindow : Window {

        public CalibrationWindow(KUKARobot robot, OptiTrackSystem optiTrack) {
            InitializeComponent();
        }

    }
}
