using System;

namespace PingPong.Applications {
    interface IApplication<T> {

        event Action Started;

        event Action Stopped;

        event Action<T> DataReady;

        void Start();

        void Stop();

        bool IsStarted();

        //void Start(IApplication application);

    }
}
