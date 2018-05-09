using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace System.Xml
{
    public static class XmlNodeExtensionMethods
    {
        private const string XmlNullKeyword = "nil";

        public static XmlDocument ToXmlDocument(this string rawXml, bool promoteEmbeddedXml = true)
        {
            const string xmlDeclarationStem = "<?xml version=";
            const string xmlDeclaration = xmlDeclarationStem + "\"1.0\" ?>";
            if (!rawXml.StartsWith(xmlDeclarationStem))
            {
                rawXml = xmlDeclaration + rawXml;
            }

            Regex docTypeRegex = new Regex(@"<!DOCTYPE .+? SYSTEM .+?>\s*");
            if (docTypeRegex.IsMatch(rawXml))
            {
                rawXml = docTypeRegex.Replace(rawXml, String.Empty);
            }

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(rawXml);

            if (promoteEmbeddedXml)
            {
                PromoteEmbeddedXml(xmlDocument.DocumentElement);
            }

            return xmlDocument;
        }

        private static void PromoteEmbeddedXml(XmlNode node)
        {
            foreach (XmlNode childNode in node.GetNestedChildren())
            {
                if (childNode.HasNestedNodes())
                {
                    PromoteEmbeddedXml(childNode);
                }
                else
                {
                    if (IsXml(childNode.InnerText))
                    {
                        childNode.InnerXml = childNode.InnerText;
                    }
                }
            }
        }

        public static bool IsXml(this string content)
        {
            if (Regex.IsMatch(content, "<.+?>", RegexOptions.Singleline))
            {
                if (Regex.IsMatch(content, "<html", RegexOptions.IgnoreCase))
                {
                    return false;
                }

                try
                {
                    XmlDocument tempDocument = new XmlDocument();
                    tempDocument.LoadXml(content);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public static List<XmlNode> GetNestedChildren(this XmlNode node)
        {
            return GetChildNodesCollection(node).Where(x => !(x is XmlText)).ToList();
        }

        public static bool HasNestedNodes(this XmlNode node)
        {
            return node.GetNestedChildren().Any();
        }

        public static bool IsEmpty(this XmlNode childNode)
        {
            return String.IsNullOrWhiteSpace(childNode.InnerText) && !childNode.IsNull();
        }

        public static bool IsNull(this XmlNode childNode)
        {
            return childNode.Attributes != null && childNode.Attributes.GetNamedItem(XmlNullKeyword) != null && childNode.Attributes[XmlNullKeyword].Value == "true";
        }

        public static bool IsList(this XmlNode node)
        {
            List<XmlNode> childNodesCollection = GetChildNodesCollection(node);
            List<string> allChildNames = childNodesCollection.Select(x => x.Name).Distinct().ToList();
            string firstChildNodeName = allChildNames.FirstOrDefault();

            bool isTypicalPlural = firstChildNodeName == node.Name.Substring(0, node.Name.Length - 1);
            bool isAtypicalPlural = false;
            if (firstChildNodeName != null && firstChildNodeName.EndsWith("y"))
            {
                isAtypicalPlural = node.Name.StartsWith(firstChildNodeName.Substring(0, firstChildNodeName.Length - 1));
            }

            return allChildNames.Count != childNodesCollection.Count || isTypicalPlural || isAtypicalPlural;
        }

        private static List<XmlNode> GetChildNodesCollection(this XmlNode node)
        {
            return node.ChildNodes.Cast<XmlNode>().ToList();
        }

        public static IEnumerable<XmlAttribute> GetAttributes(this XmlNode node)
        {
            return node.Attributes != null && node.Attributes.Count > 0
                ? node.Attributes.Cast<XmlAttribute>().Where(attribute => attribute.Name != XmlNullKeyword)
                : new List<XmlAttribute>();
        }

        public static bool IsStructuralAttribute(this XmlAttribute attribute)
        {
            string attributeName = attribute.Name.ToLower().Trim();
            return attributeName == "count" || attributeName.StartsWith("xmlns");
        }
    }
}