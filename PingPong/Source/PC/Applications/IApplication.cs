using System;

namespace PingPong.Applications {
    interface IApplication<T> where T : EventArgs {

        event EventHandler Started;

        event EventHandler Stopped;

        event EventHandler<T> DataReady;

        void Start();

        void Stop();

        bool IsStarted();

        //void Start(IApplication application);

    }
}
