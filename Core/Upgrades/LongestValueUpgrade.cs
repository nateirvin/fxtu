using System.Data;
using System.Data.SqlClient;
using XmlToTable.Core.Properties;

namespace XmlToTable.Core.Upgrades
{
    public class LongestValueUpgrade : IUpgrade
    {
        public string DatabaseScript
        {
            get { return Resources.LongestValueLengthUpgradeScript; }
        }

        public bool IsRequired(SqlConnection repositoryConnection)
        {
            int flag = repositoryConnection.GetInt32(SqlStatements.ColumnExists, new SqlParameter("@column_name", Columns.LongestValueLength));
            return flag == 0;
        }
    }
}