using MathNet.Numerics.LinearAlgebra;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PingPong.OptiTrack {
    class BallFlightEmulator {

        private readonly double x0, y0, z0;

        private readonly double vx0, vy0, vz0;

        public Action<Vector<double>, double> PositionChanged;

        public BallFlightEmulator(double x0, double y0, double z0, double vx0, double vy0, double vz0) {
            this.x0 = x0;
            this.y0 = y0;
            this.z0 = z0;

            this.vx0 = vx0;
            this.vy0 = vy0;
            this.vz0 = vz0;
        }

        public void Start(double xNoise, double yNoise, double zNoise, double simulationTime = -1) {
            Task.Run(() => {
                Random rand = new Random();

                if (simulationTime == -1) {
                    double t = 0;
                    double z;

                    do {
                        double x = x0 + vx0 * t + xNoise * (rand.NextDouble() - 0.5);
                        double y = y0 + vy0 * t + yNoise * (rand.NextDouble() - 0.5);
                        z = z0 + vz0 * t - 9.81 / 2.0 * 1000.0 * t * t + zNoise * (rand.NextDouble() - 0.5);

                        if (t > 0.2 && t < 0.3) {
                            //z -= 50;
                        }

                        Vector<double> position = Vector<double>.Build.DenseOfArray(
                            new double[] { x, y, z }
                        );

                        PositionChanged?.Invoke(position, t);
                        t += 0.004;
                        Thread.Sleep(4);
                    } while (z >= -100);
                } else {
                    for (double t = 0; t < simulationTime; t += 0.004) {
                        double x = x0 + vx0 * t + xNoise * (rand.NextDouble() - 0.5);
                        double y = y0 + vy0 * t + yNoise * (rand.NextDouble() - 0.5);
                        double z = z0 + vz0 * t - 9.81 / 2.0 * 1000.0 * t * t + zNoise * (rand.NextDouble() - 0.5);

                        Vector<double> position = Vector<double>.Build.DenseOfArray(
                            new double[] { x, y, z }
                        );

                        PositionChanged?.Invoke(position, t);
                        Thread.Sleep(4);
                    }
                }
            });
        }

    }
}
