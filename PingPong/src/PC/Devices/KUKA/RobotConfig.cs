namespace PingPong.KUKA {
    public class RobotConfig {

        /// <summary>
        /// Port defined in RSI_EthernetConfig.xml
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// robot limits
        /// </summary>
        public RobotLimits Limits { get; }

        /// <param name="port">Port defined in RSI_EthernetConfig.xml</param>
        /// <param name="limits">robot limits</param>
        public RobotConfig(int port, RobotLimits limits) {
            Port = port;
            Limits = limits;
        }

        public RobotConfig(string configFilePath) {

        }

        public void SaveToFile(string fileName) {

        }

    }
}
