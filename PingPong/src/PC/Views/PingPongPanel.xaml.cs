using System.Windows.Controls;
using PingPong.Applications;
using PingPong.KUKA;
using PingPong.OptiTrack;

namespace PingPong {
    public partial class PingPongPanel : UserControl {

        private PingPongApp pingPong;

        private PingApp ping;

        public PingPongPanel() {
            InitializeComponent();
        }

        public void InitializeApplications(KUKARobot robot1, KUKARobot robot2, OptiTrackSystem optiTrack) {
            //pingPong = new PingPongApp(robot1, robot2, optiTrack);
            //ping = new PingApp(robot1, optiTrack);
        }

    }
}
