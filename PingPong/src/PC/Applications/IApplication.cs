using System;

namespace PingPong.Applications {
    interface IApplication<T> {

        event EventHandler Started;

        event EventHandler Stopped;

        event EventHandler<T> DataReady;

        void Start();

        void Stop();

        bool IsStarted();

        //TODO: start PingPonga z wlaczanego Pinga i odwrotnie??
        //void Start(IApplication application);

    }
}
