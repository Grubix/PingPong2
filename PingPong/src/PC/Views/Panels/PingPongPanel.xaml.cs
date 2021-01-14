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
            InitializeCharts();
        }

        public void Initialize(KUKARobot robot1, KUKARobot robot2, OptiTrackSystem optiTrack) {
            if (robot1 == robot2) {
                throw new ArgumentException("Cos tam ze roboty musza byc rozne bo skutki moga byc calkiem nie ciekawe");
            }

            //pingPongApp = new PingPongApp(robot1, robot2, optiTrack, (position) => {
            //    return true; //TODO:
            //});

            robot1PingApp = new PingApp(robot1, optiTrack, (position) => {
                return position[0] < 900.0 && position[2] > 250.0;
            });

            optiTrack.Initialized += () => robot1PingApp.Start();

            //robot2PingApp = new PingApp(robot2, optiTrack, (position) => {
            //    return position[0] < 1200.0; //TODO:
            //});
        }

        private void InitializeCharts() {
            robot1PingChart.RefreshDelay = 60;
            robot1PingChart.YAxisTitle = "Ping app (robot1)";
            robot1PingChart.AddSeries("Predicted time of flight [ms]", "pr. Tf", true);
            robot1PingChart.AddSeries("Predicted ball position X [mm]", "pr. X", true);
            robot1PingChart.AddSeries("Predicted ball position Y [mm]", "pr. Y", true);
            robot1PingChart.AddSeries("Predicted ball position Z [mm]", "pr. Z", false);
            robot1PingChart.AddSeries("Ball position X [mm]", "X", false);
            robot1PingChart.AddSeries("Ball position Y [mm]", "Y", false);
            robot1PingChart.AddSeries("Ball position Z [mm]", "Z", false);
        }

    }
}