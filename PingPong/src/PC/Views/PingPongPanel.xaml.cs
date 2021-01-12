using System;
using System.Windows.Controls;
using PingPong.Applications;
using PingPong.KUKA;
using PingPong.OptiTrack;

namespace PingPong {
    public partial class PingPongPanel : UserControl {

        private PingPongApp pingPongApp;

        private PingApp robot1PingApp;

        private PingApp robot2PingApp;

        public PingPongPanel() {
            InitializeComponent();
        }

        public void InitializeApplications(KUKARobot robot1, KUKARobot robot2, OptiTrackSystem optiTrack) {
            if (robot1 == robot2) {
                throw new ArgumentException("Cos tam ze roboty musza byc rozne bo skutki moga byc calkiem nie ciekawe");
            }

            //pingPongApp = new PingPongApp(robot1, robot2, optiTrack, (position) => {
            //    return true; //TODO:
            //});

            //robot1PingApp = new PingApp(robot1, optiTrack, (position) => {
            //    return position[0] < 1200.0; //TODO:
            //});

            //robot2PingApp = new PingApp(robot2, optiTrack, (position) => {
            //    return position[0] < 1200.0; //TODO:
            //});
        }

    }
}
