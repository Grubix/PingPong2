using System;

namespace PingPong.Applications {
    class PingPongApp : IApplication<PingPongDataReadyEventArgs> {
        
        public event EventHandler Started;

        public event EventHandler Stopped;

        public event EventHandler<PingPongDataReadyEventArgs> DataReady;

        public bool IsStarted() {
            throw new NotImplementedException();
        }

        public void Start() {
            throw new NotImplementedException();
        }

        public void Stop() {
            throw new NotImplementedException();
        }

    }
}
