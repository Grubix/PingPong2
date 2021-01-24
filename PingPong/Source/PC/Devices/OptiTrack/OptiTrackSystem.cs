using NatNetML;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PingPong.OptiTrack {
    public class OptiTrackSystem : IDevice {

        private readonly NatNetClientML natNetClient;

        private bool isInitialized = false;

        private double frameTimestamp;

        public event EventHandler Initialized;

        public event EventHandler Uninitialized;

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        public ServerDescription ServerDescription { get; }

        public OptiTrackSystem(int connectionType = 0) {
            natNetClient = new NatNetClientML(connectionType);
            ServerDescription = new ServerDescription();
        }

        private void ProcessFrame(FrameOfMocapData data, NatNetClientML client) {
            double frameDeltaTime = data.fTimestamp - frameTimestamp;
            frameTimestamp = data.fTimestamp;

            var args = new FrameReceivedEventArgs {
                ReceivedFrame = new InputFrame(data, frameDeltaTime)
            };

            FrameReceived?.Invoke(this, args);
        }

        public void Initialize() {
            if (isInitialized) {
                return;
            }

            int status = natNetClient.Initialize("127.0.0.1", "127.0.0.1");

            if (status != 0) {
                throw new InvalidOperationException("OptiTrack system initialization failed. Is Motive application running?");
            }

            status = natNetClient.GetServerDescription(ServerDescription);

            if (status != 0) {
                throw new InvalidOperationException("Connection failed. Is Motive application running?");
            }

            isInitialized = true;
            Initialized?.Invoke(this, EventArgs.Empty);

            natNetClient.OnFrameReady += ProcessFrame;
        }

        public bool IsInitialized() {
            return isInitialized;
        }

        public void Uninitialize() {
            if (isInitialized) {
                isInitialized = false;
                natNetClient.OnFrameReady -= ProcessFrame;
                natNetClient.Uninitialize();

                Uninitialized?.Invoke(this, EventArgs.Empty);
            }
        }

        public List<InputFrame> WaitForFrames(int numOfFrames) {
            if (!isInitialized) {
                throw new InvalidOperationException("OptiTrack system is not initialized");
            }

            ManualResetEvent getSamplesEvent = new ManualResetEvent(false);
            var frames = new List<InputFrame>();

            void processFrame(object sender, FrameReceivedEventArgs args) {
                frames.Add(args.ReceivedFrame);

                if (frames.Count == numOfFrames) {
                    FrameReceived -= processFrame;
                    getSamplesEvent.Set();
                }
            }

            FrameReceived += processFrame;
            getSamplesEvent.WaitOne();

            return frames;
        }

    }
}