using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using XmlToTable.Core;

namespace Tests
{
    [TestFixture]
    public class SqlBuilderTests
    {
        [TestCase("[dbo].[TheTable]", "TheTable")]
        [TestCase("[dbo].[TheTable]", "dbo.TheTable")]
        [TestCase("[dbo].[TheTable]", "[dbo].TheTable")]
        [TestCase("[dbo].[TheTable]", "[dbo].[TheTable]")]
        [TestCase("[jack].[benny]", "jack.benny")]
        [TestCase("[db].[s].[t]", "db.s.t")]
        [TestCase("[db].[dbo].[table]", "db..table")]
        [TestCase("[dbo].[TheTable]", " TheTable ")]
        [TestCase("[dbo].[TheTable]", " dbo.TheTable ")]
        [TestCase("[dbo].[garg.garg]", "[garg.garg]")]
        [TestCase("[wierd.o].[garg.garg]", "[wierd.o].[garg.garg]")]
        [Description("This is done to prevent SQL injection")]
        public void BuildFromClause_ReturnsQuotedObjectName_IfNotSelectQuery(string expected, string specification)
        {
            string actual = SqlBuilder.BuildFromClause(specification);

            Assert.AreEqual(expected, actual);
        }

        [TestCase("--DROP TABLE students")]
        [TestCase("INSERT INTO dbo.Users ( 'me', 'p@55word', 'admin' )")]
        [Description("This is done to prevent SQL injection")]
        public void BuildFromClause_huh_IfIsDMLStatement(string input)
        {
            string actual = SqlBuilder.BuildFromClause(input);
            
            Console.WriteLine(actual);
            Assert.True(Regex.IsMatch(actual, @"\[[a-z0-9 -]+\]\.\[[a-z0-9- (',@)]+\]", RegexOptions.IgnoreCase));
        }

        [Test]
        public void BuildFromClause_ReturnsSubqueryClause_ForSelectStatement()
        { 
            string actual = SqlBuilder.BuildFromClause("SELECT * FROM dbo.something");

            Assert.AreEqual("(SELECT * FROM dbo.something) AS src", actual);
        }

        [Test]
        public void BuildFromClause_ReturnsSubqueryClause_ForNestedSelectStatement()
        {
            string actual = SqlBuilder.BuildFromClause("SELECT * FROM (SELECT * FROM dbo.something) AS a");

            Assert.AreEqual("(SELECT * FROM (SELECT * FROM dbo.something) AS a) AS src", actual);
        }
    }
}