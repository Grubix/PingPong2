using MathNet.Numerics.LinearAlgebra;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PingPong.Maths;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// 
        /// </summary>
        public Transformation Transformation { get; }

        /// <param name="port">Port defined in RSI_EthernetConfig.xml</param>
        /// <param name="limits">robot limits</param>
        public RobotConfig(int port, RobotLimits limits, Transformation transformation) {
            Port = port;
            Limits = limits;
            Transformation = transformation;
        }

        public RobotConfig(string jsonString) {
            var data = JObject.Parse(jsonString);

            JToken getNode(JToken parent, string nodeName) {
                var node = parent[nodeName];

                if (node == null) {
                    throw new ArgumentException($"Configuration data is invalid - '{nodeName}' node not found");
                }

                return node;
            }

            Port = (int)getNode(data, "port");

            var limitsNode = getNode(data, "limits");
            var lowerWpPointNode = getNode(limitsNode, "lowerWorkspacePoint") as JArray;
            var upperWpPointNode = getNode(limitsNode, "upperWorkspacePoint") as JArray;
            var a1LimitNode = getNode(limitsNode, "A1") as JArray;
            var a2LimitNode = getNode(limitsNode, "A2") as JArray;
            var a3LimitNode = getNode(limitsNode, "A3") as JArray;
            var a4LimitNode = getNode(limitsNode, "A4") as JArray;
            var a5LimitNode = getNode(limitsNode, "A5") as JArray;
            var a6LimitNode = getNode(limitsNode, "A6") as JArray;
            var maxCorrectionXYZNode = getNode(limitsNode, "maxCorrectionXYZ");
            var maxCorrectionABCNode = getNode(limitsNode, "maxCorrectionABC");

            Limits = new RobotLimits(
                ((double)lowerWpPointNode[0], (double)lowerWpPointNode[1], (double)lowerWpPointNode[2]),
                ((double)upperWpPointNode[0], (double)upperWpPointNode[1], (double)upperWpPointNode[2]),
                ((double)a1LimitNode[0], (double)a1LimitNode[1]),
                ((double)a2LimitNode[0], (double)a2LimitNode[1]),
                ((double)a3LimitNode[0], (double)a3LimitNode[1]),
                ((double)a4LimitNode[0], (double)a4LimitNode[1]),
                ((double)a5LimitNode[0], (double)a5LimitNode[1]),
                ((double)a6LimitNode[0], (double)a6LimitNode[1]),
                ((double)maxCorrectionXYZNode, (double)maxCorrectionABCNode)
            );

            var transformationNode = getNode(data, "transformation") as JArray;
            var row0Node = transformationNode[0] as JArray;
            var row1Node = transformationNode[1] as JArray;
            var row2Node = transformationNode[2] as JArray;

            var rotation = Matrix<double>.Build.DenseOfArray(new double[,] {
                { (double)row0Node[0], (double)row0Node[1], (double)row0Node[2] },
                { (double)row1Node[0], (double)row1Node[1], (double)row1Node[2] },
                { (double)row2Node[0], (double)row2Node[1], (double)row2Node[2] },
            });

            var translation = Vector<double>.Build.DenseOfArray(new double[] {
                (double)row0Node[3], (double)row1Node[3], (double)row2Node[3]
            });

            Transformation = new Transformation(rotation, translation);
        }

        public void SaveToFile() {
            (double wx0, double wy0, double wz0) = Limits.LowerWorkspacePoint;
            (double wx1, double wy1, double wz1) = Limits.UpperWorkspacePoint;

            string jsonString =
            $@"{{
                ""port"": {Port},
                ""limits"": {{
                    ""lowerWorkspacePoint"": [{wx0}, {wy0}, {wz0}],
                    ""upperWorkspacePoint"": [{wx1}, {wy1}, {wz1}],
                    ""A1"": [{Limits.A1AxisLimit.Min}, {Limits.A1AxisLimit.Max}],
                    ""A2"": [{Limits.A2AxisLimit.Min}, {Limits.A2AxisLimit.Max}],
                    ""A3"": [{Limits.A3AxisLimit.Min}, {Limits.A3AxisLimit.Max}],
                    ""A4"": [{Limits.A4AxisLimit.Min}, {Limits.A4AxisLimit.Max}],
                    ""A5"": [{Limits.A5AxisLimit.Min}, {Limits.A5AxisLimit.Max}],
                    ""A6"": [{Limits.A6AxisLimit.Min}, {Limits.A6AxisLimit.Max}],
                    ""maxCorrectionXYZ"": {Limits.CorrectionLimit.XYZ},
                    ""maxCorrectionABC"": {Limits.CorrectionLimit.ABC}
                }},
                ""transformation"": [
                    [{Transformation[0, 0]}, {Transformation[0, 1]}, {Transformation[0, 2]}, {Transformation[0, 3]}],
                    [{Transformation[1, 0]}, {Transformation[1, 1]}, {Transformation[1, 2]}, {Transformation[1, 3]}],
                    [{Transformation[2, 0]}, {Transformation[2, 1]}, {Transformation[2, 2]}, {Transformation[2, 3]}],
                    [{Transformation[3, 0]}, {Transformation[3, 1]}, {Transformation[3, 2]}, {Transformation[3, 3]}]
                ]
            }}";

            //TODO: C# ogolnie jest spoko, ale to jest jakies uposledzone i nwm jak to zrobic inaczej ¯\_(ツ)_/¯
            jsonString = Regex.Replace(jsonString, @"\n( {4}){3}", "\n");

            SaveFileDialog saveFileDialog1 = new SaveFileDialog {
                InitialDirectory = Directory.GetCurrentDirectory(),
                CheckPathExists = true,
                FilterIndex = 2,
                Title = "Save configuration file",
                DefaultExt = "json",
                Filter = "JSON files |*.json",
                FileName = "robot.config.json"
            };

            if((bool)saveFileDialog1.ShowDialog() == true && saveFileDialog1.FileName != "") {
                File.WriteAllText(saveFileDialog1.FileName, jsonString);
            }
        }

    }
}
