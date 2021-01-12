using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.OptiTrack;
using System;

namespace PingPong.Applications {
    class PingPongApp : IApplication {

        private bool isStarted;

        private readonly KUKARobot robot1;

        private readonly KUKARobot robot2;

        private readonly OptiTrackSystem optiTrack;

        private readonly Func<Vector<double>, bool> checkFunction;

        public PingPongApp(KUKARobot robot1, KUKARobot robot2, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
            this.robot1 = robot1;
            this.robot2 = robot2;
            this.optiTrack = optiTrack;
            this.checkFunction = checkFunction;
        }

        ~PingPongApp() {
            Stop();
        }

        public void Start() {
            isStarted = true;
            //TODO:
        }

        public void Stop() {
            isStarted = false;
            //TODO:
        }

        public bool IsStarted() {
            return isStarted;
        }

    }
}
