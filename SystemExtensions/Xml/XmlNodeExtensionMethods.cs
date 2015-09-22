﻿using System.Collections.Generic;
using System.Linq;

namespace System.Xml
{
    public static class XmlNodeExtensionMethods
    {
        private const string XmlNullKeyword = "nil";

        public static XmlDocument ToXmlDocument(this string rawXml)
        {
            const string xmlDeclarationStem = "<?xml version=\"";
            const string xmlDeclaration = xmlDeclarationStem + "1.0\" ?>";
            if (!rawXml.StartsWith(xmlDeclarationStem))
            {
                rawXml = xmlDeclaration + rawXml;
            }

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(rawXml);

            return xmlDocument;
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

            return (allChildNames.Count != childNodesCollection.Count) || isTypicalPlural || isAtypicalPlural;
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
    }
}