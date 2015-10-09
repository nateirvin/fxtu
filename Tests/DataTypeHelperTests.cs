using System;
using System.Data;
using NUnit.Framework;
using XmlToTable.Core;

namespace Tests
{
    [TestFixture]
    public class DataTypeHelperTests
    {
        private DataTypeHelper _classUnderTest;

        [SetUp]
        public void RunBeforeEachTest()
        {
            _classUnderTest = new DataTypeHelper();
        }

        [TestCase(null, null, null)]
        [TestCase(SqlDbType.NVarChar, null, "zero")]

        [TestCase(SqlDbType.Bit, null, "true")]
        [TestCase(SqlDbType.Bit, null, "0")]
        [TestCase(SqlDbType.UniqueIdentifier, null, "B7D42137-1A04-4854-ADC4-DB573D0E11D2")]
        [TestCase(SqlDbType.BigInt, null, "34359738368")] 
        [TestCase(SqlDbType.Int, null, "8456156")] 
        [TestCase(SqlDbType.Float, null, "123.45")]
        [TestCase(SqlDbType.DateTime, null, "3/1/2015")]
        [TestCase(SqlDbType.NVarChar, null, "aloha!")]

        [TestCase(SqlDbType.Bit, SqlDbType.Bit, null)]
        [TestCase(SqlDbType.UniqueIdentifier, SqlDbType.UniqueIdentifier, null)]
        [TestCase(SqlDbType.BigInt, SqlDbType.BigInt, null)]
        [TestCase(SqlDbType.Int, SqlDbType.Int, null)]
        [TestCase(SqlDbType.Float, SqlDbType.Float, null)]
        [TestCase(SqlDbType.DateTime, SqlDbType.DateTime, null)]
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, null)]

        [TestCase(SqlDbType.Bit, SqlDbType.Bit, "false")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.Bit, "not true")]

        [TestCase(SqlDbType.UniqueIdentifier, SqlDbType.UniqueIdentifier, "B7D42137-1A04-4854-ADC4-DB573D0E11D2")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.UniqueIdentifier, "zimbabwe")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.UniqueIdentifier, "123.45")]

        [TestCase(SqlDbType.BigInt, SqlDbType.BigInt, "34359738368")]
        [TestCase(SqlDbType.BigInt, SqlDbType.Int, "34359738368")]
        [TestCase(SqlDbType.Float, SqlDbType.Float, "34359738368")]

        [TestCase(SqlDbType.Int, SqlDbType.Int, "8456156")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.Bit, "8456156")]
        [TestCase(SqlDbType.BigInt, SqlDbType.BigInt, "8456156")]
        [TestCase(SqlDbType.BigInt, SqlDbType.BigInt, "8456156")]
        [TestCase(SqlDbType.Float, SqlDbType.Float, "8456156")]

        [TestCase(SqlDbType.DateTime, SqlDbType.DateTime, "3/1/2015")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.Float, "3/1/2015")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.BigInt, "3/1/2015")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.Int, "3/1/2015")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.Bit, "3/1/2015")]

        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "true")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "B7D42137-1A04-4854-ADC4-DB573D0E11D2")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "34359738368")]  //Bigint
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "8456156")]  //Int
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "123.45")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "3/1/2015")]
        [TestCase(SqlDbType.NVarChar, SqlDbType.NVarChar, "aloha!")]

        [TestCase(SqlDbType.NVarChar, SqlDbType.DateTime, "false")]
        public void SuggestType(SqlDbType? expected, SqlDbType? current, string newValue)
        {
            Assert.AreEqual(expected, _classUnderTest.SuggestType(current, newValue));
        }

        [Test]
        public void ConvertTo_CanConvertToGuid()
        {
            string rawValue = "ec0e9af3580e565f20109609f717cd62";
            Guid expected = Guid.Parse(rawValue);

            object actual = _classUnderTest.ConvertTo(typeof (Guid), rawValue);

            Assert.IsInstanceOf<Guid>(actual);
            Assert.AreEqual(expected, actual);
        }
    }
}