using System.Collections.Generic;
using System.Data.SqlClient;
using System.Xml;
using XmlToTable.Core.Upgrades;

namespace XmlToTable.Core
{
    public interface IXmlToTableAdapter
    {
        string DatabaseCreationScript { get; }
        IEnumerable<IUpgrade> Upgrades { get; }
        void Initialize(SqlConnection repositoryConnection);
        void ImportDocument(string documentId, string providerName, XmlDocument content);
        void SaveChanges(SqlTransactionExtended transaction);
    }
}