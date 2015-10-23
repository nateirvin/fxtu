using System.Data.SqlClient;

namespace XmlToTable.Core.Upgrades
{
    public interface IUpgrade
    {
        string DatabaseScript { get; }
        bool IsRequired(SqlConnection repositoryConnection);
    }
}