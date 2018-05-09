using System.Xml;

namespace XmlToTable.Core.Sources
{
    internal class XmlParser
    {
        public static object TryParseXml(string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return null;
            }

            try
            {
                return rawContent.ToXmlDocument();
            }
            catch (XmlException xmlException)
            {
                return xmlException;
            }
        }
    }
}