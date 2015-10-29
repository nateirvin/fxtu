using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class ExtensionMethodTests
    {
        [Test]
        public void ToXmlDocument_ParsesCorrectly_IfContentDoesNotContainHeader()
        {
            string input = "<root></root>";

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            StringAssert.StartsWith("<?xml version=", actual.InnerXml);
        }

        [TestCase("\"")]
        [TestCase("'")]
        public void ToXmlDocument_ParsesCorrectly_IfContentContainsExactHeader(string delimiter)
        {
            string input = String.Format("<?xml version={0}1.0{0} ?><root></root>", delimiter);

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            StringAssert.StartsWith("<?xml version=", actual.InnerXml);
        }

        [TestCase("\"")]
        [TestCase("'")]
        public void ToXmlDocument_ParsesCorrectly_IfContentContainsHeader(string delimiter)
        {
            string input = String.Format("<?xml version={0}1.0{0} encoding={0}utf-16{0} ?><root></root>", delimiter);

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            StringAssert.StartsWith("<?xml version=", actual.InnerXml);
        }

        [Test]
        public void ToXmlDocument_ParsesCorrectly_IfContentContainsPrivateExternalDTDReference()
        {
            string input = @"<?xml version='1.0' encoding='UTF-8'?>
                <!DOCTYPE xgdresponse SYSTEM 'xgdresponse.dtd'>
                <xgdresponse version='1.0'>
	                <transid>0</transid>
	                <errorcode>65</errorcode>
                </xgdresponse>";

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            Assert.IsNotNull(actual.DocumentElement);
            Assert.AreEqual("xgdresponse", actual.DocumentElement.Name);
            Assert.True(actual.DocumentElement.HasChildNodes);
        }

        [Test]
        public void ToXmlDocument_UnescapesEmbeddedXml()
        {
            string input = File.ReadAllText("embedded_xml_example.xml");

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            Assert.IsNotNull(actual.DocumentElement);
            XmlNode valueNode = actual.DocumentElement.ChildNodes[2].ChildNodes[0].ChildNodes[2];
            List<XmlNode> valueChildren = valueNode.ChildNodes.Cast<XmlNode>().Where(x => x.NodeType != XmlNodeType.Text).ToList();
            Assert.AreEqual(1, valueChildren.Count);
            Assert.AreEqual("Contact", valueChildren.First().Name);
            Assert.False(valueNode.InnerText.Contains("<"));
            Assert.AreEqual("Names", valueChildren.First().ChildNodes[0].Name);
        }

        [Test]
        public void ToXmlDocument_DoesNotTryToUnescapeXmlLikeContent()
        {
            string input = File.ReadAllText("example.xml");

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            Assert.IsNotNull(actual.DocumentElement);
            XmlNode messageNode = actual.DocumentElement.ChildNodes[1].ChildNodes[2].ChildNodes[0].ChildNodes[0];
            Assert.AreEqual("Invalid format. One or more data fields contain invalid data. The <hint> value inside the error block contains which element(s) have invalid data.", messageNode.InnerText);
            Assert.False(messageNode.HasNestedNodes());
        }
    }
}