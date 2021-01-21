using System;

namespace PingPong.Applications {
    interface IApplication<T> {

        event Action Started;

        event Action Stopped;

        event Action<T> DataReady;

        void Start();

        void Stop();

        bool IsStarted();

        //TODO: start PingPonga z wlaczanego Pinga i odwrotnie??
        //void Start(IApplication application);

    }
}
