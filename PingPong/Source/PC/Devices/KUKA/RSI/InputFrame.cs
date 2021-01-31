using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace PingPong.KUKA {
    /// <summary>
    /// Represents frame (data) received from the KUKA robot
    /// </summary>
    public class InputFrame {

        private class Tag {

            public string Value { get; private set; }

            private readonly NameValueCollection attributes;

            public string this[string attributeName] {
                get {
                    string value = attributes[attributeName];

                    if (value == null) {
                        throw new ArgumentException($"Attribute '{attributeName}' not found");
                    }

                    return value;
                }
            }

            public Tag(string data, string tag) {
                Regex tagRegex = new Regex($"<{tag}([^/>]*)/?>(([^<]*)</{tag}>)?");
                Match match = tagRegex.Match(data);

                if (match.Success) {
                    Value = match.Groups[3].Value.Trim();
                    attributes = ExtractAttributes(match.Groups[1].Value.Trim());
                } else {
                    throw new ArgumentException($"Tag <{tag}> not found in data");
                }
            }

            private NameValueCollection ExtractAttributes(string attributesString) {
                NameValueCollection attributes = new NameValueCollection();

                if (string.IsNullOrEmpty(attributesString)) {
                    return attributes;
                }

                Regex attributeRegex = new Regex("([a-zA-Z0-9_]+)[ ]*=[ ]*\"([^\"]*)\"");
                MatchCollection matches = attributeRegex.Matches(attributesString);

                foreach (Match match in matches) {
                    attributes[match.Groups[1].Value.Trim()] = match.Groups[2].Value;
                }

                return attributes;
            }

        }

        /// <summary>
        /// Time stamp
        /// </summary>
        public long IPOC { get; set; }

        /// <summary>
        /// Current cartesian position
        /// </summary>
        public RobotVector Position { get; set; }

        /// <summary>
        /// Current axis position
        /// </summary>
        public RobotAxisVector AxisPosition { get; set; }

        public InputFrame() {
        }

        public InputFrame(string data) {
            IPOC = long.Parse(new Tag(data, "IPOC").Value);
            Position = ExtractPosition(new Tag(data, "RIst"));
            AxisPosition = ExtractAxisPosition(new Tag(data, "AIPos"));
        }

        private RobotVector ExtractPosition(Tag tag) {
            double X = double.Parse(tag["X"]);
            double Y = double.Parse(tag["Y"]);
            double Z = double.Parse(tag["Z"]);
            double A = double.Parse(tag["A"]);
            double B = double.Parse(tag["B"]);
            double C = double.Parse(tag["C"]);

            //A = A < 0 ? 360.0 + A : A;
            //B = B < 0 ? 360.0 + B : B;
            //C = C < 0 ? 360.0 + C : C;

            return new RobotVector(X, Y, Z, A, B, C);
        }

        private RobotAxisVector ExtractAxisPosition(Tag tag) {
            double A1 = double.Parse(tag["A1"]);
            double A2 = double.Parse(tag["A2"]);
            double A3 = double.Parse(tag["A3"]);
            double A4 = double.Parse(tag["A4"]);
            double A5 = double.Parse(tag["A5"]);
            double A6 = double.Parse(tag["A6"]);

            return new RobotAxisVector(A1, A2, A3, A4, A5, A6);
        }

    }
}