using PingPong.KUKA;
using PingPong.OptiTrack;

namespace PingPong.Applications {
    class PingPongApplication : IApplication {

        private KUKARobot robot1;

        private KUKARobot robot2;

        private OptiTrackSystem optiTrack;

        public PingPongApplication(KUKARobot robot1, KUKARobot robot2, OptiTrackSystem optiTrack) {
            this.robot1 = robot1;
            this.robot2 = robot2;
            this.optiTrack = optiTrack;
        }

        public void Start() {
            //TODO:
        }

        public void Stop() {
            //TODO:
        }

    }
}
