using System.Data.SqlClient;
using System.Xml;

namespace XmlToTable.Core
{
    public interface IXmlToTableAdapter
    {
        string DatabaseCreationScript { get; }
        void Initialize(SqlConnection repositoryConnection);
        void ImportDocument(int documentId, string providerName, XmlDocument content);
        void SaveChanges(SqlTransactionExtended transaction);
    }
}