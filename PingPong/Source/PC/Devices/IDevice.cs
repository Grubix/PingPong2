namespace PingPong {
    interface IDevice {

        ///<summary>
        ///Initializes device
        ///</summary>
        void Initialize();

        /// <summary>
        /// Uninitializes device
        /// </summary>
        void Uninitialize();

        /// <summary>
        /// Indicates if device has been initialized
        /// </summary>
        /// <returns>true if device is ready to use, false otherwise</returns>
        bool IsInitialized();

    }
}