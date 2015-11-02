using System.Data;
using System.Data.SqlClient;
using XmlToTable.Core.Properties;

namespace XmlToTable.Core.Upgrades
{
    public class NumberToTextUpgrade : IUpgrade
    {
        public string DatabaseScript
        {
            get { return Resources.NumberToTextCorrectionStatement; }
        }

        public bool IsRequired(SqlConnection repositoryConnection)
        {
            int objectId = repositoryConnection.GetInt32(SqlStatements.GetObjectId, new SqlParameter("@ObjectName", SqlStatements.usp_GetExtendedProperties));
            return objectId == 0;
        }
    }
}