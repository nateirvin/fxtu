using System;
using NUnit.Framework;
using XmlToTable.Core;

namespace Tests
{
    [TestFixture]
    public class NameHandlerTests
    {
        private NameHandler _classUnderTest;

        [SetUp]
        public void RunBeforeEachTest()
        {
            _classUnderTest = new NameHandler();
        }

        [TestCase(-1)]
        [TestCase(0)]
        [ExpectedException(typeof(ArgumentException))]
        public void GetValidName_ThrowsException_IfMaxLengthIsLessThanOne(int maxLength)
        {
            _classUnderTest.GetValidName("hi", maxLength, TooLongNameBehavior.Truncate);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GetValidName_ReturnsInput_IfInputIsNullOrWhitespace(string input)
        {
            string actual = _classUnderTest.GetValidName(input, 50, TooLongNameBehavior.Truncate);

            Assert.AreEqual(input, actual);
        }

        [Test]
        public void GetValidName_ReturnsInput_IfShouldTruncate_AlreadyShortEnough()
        {
            const string input = "hi";

            string actual = _classUnderTest.GetValidName(input, 50, TooLongNameBehavior.Truncate);

            Assert.AreEqual(input, actual);
        }

        [Test]
        public void GetValidName_Truncates_IfTooLongInTruncateMode()
        {
            const string input = "hi";

            string actual = _classUnderTest.GetValidName(input, 1, TooLongNameBehavior.Truncate);

            Assert.AreEqual("h", actual);
        }

        [Test]
        public void GetValidName_ReturnsInput_IfShouldThrow_AlreadyShortEnough()
        {
            const string input = "hi";

            string actual = _classUnderTest.GetValidName(input, 50, TooLongNameBehavior.Throw);

            Assert.AreEqual(input, actual);
        }

        [Test]
        [ExpectedException(typeof(InvalidNameException))]
        public void GetValidName_Throws_IfTooLongInThrowMode()
        {
            _classUnderTest.GetValidName("hi", 1, TooLongNameBehavior.Throw);
        }

        [Test]
        public void GetValidName_ReturnsInput_AbbreviationMode_NotTooLong()
        {
            const string input = "h";

            string actual = _classUnderTest.GetValidName(input, 32, TooLongNameBehavior.Abbreviate);

            Assert.AreEqual("h", actual);
        }

        [Test]
        public void GetValidName_ReturnsSuperAbbreviatedVersion_IfReallyLongInAbbreviationMode_DashSeparated()
        {
            string actual = _classUnderTest.GetValidName("secondary-accounts-failed-payments-payday-tradeline", 32, TooLongNameBehavior.Abbreviate);

            // ReSharper disable once StringLiteralTypo
            Assert.AreEqual("scndryaccntsfldpymntspydytrdln", actual);
        }

        [Test]
        public void GetValidName_ReturnsAbbreviatedVersion_IfTooLongInAbbreviationMode_DashSeparated()
        {
            string actual = _classUnderTest.GetValidName("clear-subprime-idfraud-validation", 32, TooLongNameBehavior.Abbreviate);

            Assert.AreEqual("clr-subprime-idfraud-validation", actual);
        }

        [Test]
        [ExpectedException(typeof(InvalidNameException))]
        public void GetValidName_Throws_IfTooLongInAbbreviationMode_CannotAbbreviate()
        {
            _classUnderTest.GetValidName("secondary-accounts-failed-payments-payday-tradeline", 12, TooLongNameBehavior.Abbreviate);
        }
    }
}