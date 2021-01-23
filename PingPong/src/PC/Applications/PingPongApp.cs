using MathNet.Numerics.LinearAlgebra;
using PingPong.KUKA;
using PingPong.OptiTrack;
using System;

namespace PingPong.Applications {
    class PingPongApp : IApplication<PingPongDataReadyEventArgs> {

        private bool isStarted;

        private readonly Robot robot1;

        private readonly Robot robot2;

        private readonly OptiTrackSystem optiTrack;

        private readonly Func<Vector<double>, bool> checkFunction;

        public event EventHandler Started;

        public event EventHandler Stopped;

        public event EventHandler<PingPongDataReadyEventArgs> DataReady;

        public PingPongApp(Robot robot1, Robot robot2, OptiTrackSystem optiTrack, Func<Vector<double>, bool> checkFunction) {
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
