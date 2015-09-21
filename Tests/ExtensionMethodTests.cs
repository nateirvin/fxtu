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

        [Test]
        public void ToXmlDocument_ParsesCorrectly_IfContentContainsExactHeader()
        {
            string input = "<?xml version=\"1.0\" ?><root></root>";

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            StringAssert.StartsWith("<?xml version=", actual.InnerXml);
        }

        [Test]
        public void ToXmlDocument_ParsesCorrectly_IfContentContainsHeader()
        {
            string input = "<?xml version=\"1.0\" encoding=\"utf-16\" ?><root></root>";

            XmlDocument actual = input.ToXmlDocument();

            Assert.IsNotNull(actual);
            StringAssert.StartsWith("<?xml version=", actual.InnerXml);
        }
    }
}