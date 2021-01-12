namespace PingPong.Applications {
    interface IApplication {

        void Start();

        //void Start(IApplication application);

        void Stop();

        bool IsStarted();

    }
}
