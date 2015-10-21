using System.Data.SqlClient;
using System.Xml;

namespace XmlToTable.Core
{
    public interface IXmlToTableAdapter
    {
        string DatabaseCreationScript { get; }
        string DatabaseUpgradeScript { get; }
        void Initialize(SqlConnection repositoryConnection);
        bool RequiresUpgrade(SqlConnection repositoryConnection);
        void ImportDocument(int documentId, string providerName, XmlDocument content);
        void SaveChanges(SqlTransactionExtended transaction);
    }
}