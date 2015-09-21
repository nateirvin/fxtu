using System.Data;

namespace XmlToTable.Core
{
    public class DataForeignKey
    {
        public DataForeignKey(string constraintName, DataColumn parentColumn, DataColumn childColumn)
        {
            ConstraintName = constraintName;
            ParentColumn = parentColumn;
            ChildColumn = childColumn;
            ObjectState = PersistenceState.NotCreated;
        }

        public string ConstraintName { get; set; }

        public DataTable ParentTable
        {
            get { return ParentColumn.Table; }
        }

        public DataColumn ParentColumn { get; set; }
        public DataColumn ChildColumn { get; set; }

        public PersistenceState ObjectState { get; set; }
    }
}