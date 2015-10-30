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
            return true;
        }
    }
}