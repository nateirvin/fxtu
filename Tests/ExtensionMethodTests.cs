using System;
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
    }
}