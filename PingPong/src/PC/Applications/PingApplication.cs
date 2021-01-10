using PingPong.KUKA;
using PingPong.OptiTrack;

namespace PingPong.Applications {
    class PingApplication : IApplication {

        private KUKARobot robot;

        private OptiTrackSystem optiTrack;

        public PingApplication(KUKARobot robot, OptiTrackSystem optiTrack) {
            this.robot = robot;
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
