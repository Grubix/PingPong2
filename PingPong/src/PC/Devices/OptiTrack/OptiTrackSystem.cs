using MathNet.Numerics.LinearAlgebra;
using NatNetML;
using System;
using System.Threading;

namespace PingPong.OptiTrack {
    public class OptiTrackSystem : IDevice {

        private readonly NatNetClientML natNetClient;

        private readonly ServerDescription serverDescription;

        private bool isInitialized = false;

        private double frameTimestamp;

        public event Action Initialized;

        public event Action<InputFrame> FrameReceived;

        public OptiTrackSystem(int connectionType = 0) {
            natNetClient = new NatNetClientML(connectionType);
            serverDescription = new ServerDescription();
        }

        public void Initialize() {
            if (isInitialized) {
                return;
            }

            int status = natNetClient.Initialize("127.0.0.1", "127.0.0.1");

            if (status != 0) {
                throw new InvalidOperationException("OptiTrack system initialization failed. Is Motive application running?");
            }

            status = natNetClient.GetServerDescription(serverDescription);

            if (status != 0) {
                throw new InvalidOperationException("Connection failed. Is Motive application running?");
            }

            isInitialized = true;
            Initialized?.Invoke();

            natNetClient.OnFrameReady += (data, client) => {
                double frameDeltaTime = data.fTimestamp - frameTimestamp;
                frameTimestamp = data.fTimestamp;
                FrameReceived?.Invoke(new InputFrame(data, frameDeltaTime));
            };
        }

        public bool IsInitialized() {
            return isInitialized;
        }

        public void Uninitialize() {
            isInitialized = false;
            natNetClient.Uninitialize();
        }

        // TODO: async, token source
        public Vector<double> GetAveragePosition(int samples) {
            if (!isInitialized) {
                throw new InvalidOperationException("OptiTrack system is not initialized");
            }

            ManualResetEvent getSamplesEvent = new ManualResetEvent(false);
            var position = Vector<double>.Build.Dense(3);

            int currentSample = 0;

            void checkSample(InputFrame inputFrame) {
                position += inputFrame.BallPosition;
                currentSample++;

                if (currentSample >= samples) {
                    FrameReceived -= checkSample;
                    getSamplesEvent.Set();
                }
            }

            FrameReceived += checkSample;
            getSamplesEvent.WaitOne();

            return position / samples;
        }

    }
}
