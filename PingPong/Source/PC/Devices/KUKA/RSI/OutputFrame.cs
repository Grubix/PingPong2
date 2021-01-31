using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PingPong.KUKA {
    /// <summary>
    /// Represents frame (data) sent to the KUKA robot
    /// </summary>
    public class OutputFrame {

        private static readonly string frameTemplate = @"
            <Sen Type='PingPong'>
                <EStr>{0}</EStr>
                <RKorr X='{1}' Y='{2}' Z='{3}' A='{4}' B='{5}' C='{6}' />
                <IPOC>{7}</IPOC>
            </Sen>";

        /// <summary>
        /// Minifies frame template (removes new lines, indentation, redundant white characters etc.)
        /// </summary>
        static OutputFrame() {
            XDocument document = XDocument.Parse(frameTemplate);
            StringBuilder sBuilder = new StringBuilder();
            XmlWriterSettings xmlSettings = new XmlWriterSettings() {
                OmitXmlDeclaration = true
            };

            using (XmlWriter xmlWriter = XmlWriter.Create(sBuilder, xmlSettings)) {
                document.Root.Save(xmlWriter);
            }

            frameTemplate = sBuilder.ToString();
        }

        public string Message { get; set; } = "Ping Pong";

        public RobotVector Correction { get; set; } = RobotVector.Zero;

        public long IPOC { get; set; }

        public override string ToString() {
            return string.Format(frameTemplate,
                Message,
                Correction.X,
                Correction.Y,
                Correction.Z,
                Correction.A,
                Correction.B,
                Correction.C,
                IPOC
            );
        }

    }
}