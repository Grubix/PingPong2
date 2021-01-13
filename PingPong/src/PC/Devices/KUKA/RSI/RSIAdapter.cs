using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PingPong.KUKA {
    /// <summary>
    /// Provides methods for receiving and sending data to the KUKA robot
    /// with RSI (Robot Sensor Interface) installed
    /// </summary>
    public class RSIAdapter {

        private UdpClient client;

        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// Remote endpoint Ip (Robot Sensor Interface - RSI)
        /// </summary>
        public string Ip {
            get {
                if (remoteEndPoint != null) {
                    return remoteEndPoint.Address.ToString();
                } else {
                    return null;
                }
            }
        }

        public RSIAdapter() {
        }

        /// <summary>
        /// Connects to robot and returns the first received frame
        /// </summary>
        /// <returns>first received frame</returns>
        public async Task<InputFrame> Connect(int port) {
            client = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            UdpReceiveResult result = await client.ReceiveAsync();
            remoteEndPoint = result.RemoteEndPoint;
            byte[] receivedBytes = result.Buffer;

            return new InputFrame(Encoding.ASCII.GetString(receivedBytes, 0, receivedBytes.Length));
        }

        /// <summary>
        /// Close connection
        /// </summary>
        public void Disconnect() {
            if (client != null) {
                client.Close();
                client = null;
            }
        }

        /// <summary>
        /// Receives data from the remoteEndPoint (KUKA robot) asynchronously
        /// </summary>
        /// <returns>parsed data as InputFrame</returns>
        public async Task<InputFrame> ReceiveDataAsync() {
            UdpReceiveResult result = await client.ReceiveAsync();
            byte[] receivedBytes = result.Buffer;

            return new InputFrame(Encoding.ASCII.GetString(receivedBytes, 0, receivedBytes.Length));
        }

        /// <summary>
        /// Sends data to the remoteEndPoint (KUKA robot) 
        /// </summary>
        /// <param name="data">data to sent</param>
        public void SendData(OutputFrame data) {
            byte[] bytes = Encoding.ASCII.GetBytes(data.ToString());
            client.Send(bytes, bytes.Length, remoteEndPoint);
        }

    }
}