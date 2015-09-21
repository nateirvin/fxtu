using System.Data;
using System.Text;
using System.Xml;
using NUnit.Framework;
using XmlToTable.Core;

namespace Tests
{
    [TestFixture]
    public class HierarchicalModelTests
    {
        private HierarchicalModel _classUnderTest;

        [SetUp]
        public void RunBeforeEachTest()
        {
            _classUnderTest = new HierarchicalModel();
        }

        [TestCase(PersistenceState.NotCreated)]
        [TestCase(PersistenceState.PendingChanges)]
        [TestCase(PersistenceState.Written)]
        public void AdjustMaximumLength_DoesNotChangePersistenceState_IfColumnLengthExpectationDidNotChange(PersistenceState persistenceState)
        {
            string xml = "<hi>there</hi>";
            DataTable table = new DataTable();
            DataColumn column = table.Columns.Add("name", typeof (string));
            column.MaxLength = 105;
            column.ExtendedProperties.Add(HierarchicalModel.WriteStatePropertyName, persistenceState);
            
            _classUnderTest.AdjustDataType(table, table.Columns[0].ColumnName, xml.ToXmlDocument().DocumentElement.InnerText);

            Assert.AreEqual(persistenceState, table.Columns[0].ExtendedProperties.GetValue<PersistenceState>(HierarchicalModel.WriteStatePropertyName));
        }

        [Test]
        public void AdjustMaximumLength_SetsPendingChangesState_IfColumnLengthExpectationExpanded_ForExistingColumn()
        {
            string xml = "<hi>there12345</hi>";
            DataTable table = new DataTable();
            DataColumn column = table.Columns.Add("name", typeof(string));
            column.MaxLength = 105;
            column.ExtendedProperties.Add(HierarchicalModel.WriteStatePropertyName, PersistenceState.Written);

            _classUnderTest.AdjustDataType(table, table.Columns[0].ColumnName, xml.ToXmlDocument().DocumentElement.InnerText);

            Assert.AreEqual(PersistenceState.PendingChanges, table.Columns[0].ExtendedProperties.GetValue<PersistenceState>(HierarchicalModel.WriteStatePropertyName));
        }

        [Test]
        public void AdjustMaximumLength_DoesNotChangePersistenceState_IfColumnLengthExpectationExpanded_ForNewColumn()
        {
            string xml = "<hi>there12345</hi>";
            DataTable table = new DataTable();
            DataColumn column = table.Columns.Add("name", typeof(string));
            column.MaxLength = 105;
            column.ExtendedProperties.Add(HierarchicalModel.WriteStatePropertyName, PersistenceState.NotCreated);

            _classUnderTest.AdjustDataType(table, table.Columns[0].ColumnName, xml.ToXmlDocument().DocumentElement.InnerText);

            Assert.AreEqual(PersistenceState.NotCreated, table.Columns[0].ExtendedProperties.GetValue<PersistenceState>(HierarchicalModel.WriteStatePropertyName));
        }

        [Test]
        public void AdjustMaximumLength_DoesNotChangePersistenceState_IfColumnLengthExpectationExpanded_ForDirtyColumn()
        {
            string xml = "<hi>there12345</hi>";
            DataTable table = new DataTable();
            DataColumn column = table.Columns.Add("name", typeof(string));
            column.MaxLength = 105;
            column.ExtendedProperties.Add(HierarchicalModel.WriteStatePropertyName, PersistenceState.PendingChanges);

            _classUnderTest.AdjustDataType(table, table.Columns[0].ColumnName, xml.ToXmlDocument().DocumentElement.InnerText);

            Assert.AreEqual(PersistenceState.PendingChanges, table.Columns[0].ExtendedProperties.GetValue<PersistenceState>(HierarchicalModel.WriteStatePropertyName));
        }

        [Test]
        public void AdjustMaximumLength_AdjustMaxLengthToIntMaxValue_IfLengthExceeds4000_ForNVarchar()
        {
            DataTable table = new DataTable();
            DataColumn column = table.Columns.Add("name", typeof(string));
            column.MaxLength = 105;
            column.ExtendedProperties.Add(HierarchicalModel.WriteStatePropertyName, PersistenceState.Written);
            StringBuilder rawXml = new StringBuilder("<hi>");
            for (int i = 0; i < 5000; i++)
            {
                rawXml.Append("a");
            }
            rawXml.Append("</hi>");
            XmlNode xmlNode = rawXml.ToString().ToXmlDocument().DocumentElement;

            _classUnderTest.AdjustDataType(table, table.Columns[0].ColumnName, xmlNode.InnerText);

            DataColumn actual = table.Columns[0];
            Assert.AreEqual(int.MaxValue, actual.MaxLength);
        }
    }
}