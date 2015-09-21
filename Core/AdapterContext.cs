using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Xml;

namespace XmlToTable.Core
{
    public class AdapterContext : IXmlToTableAdapter
    {
        private readonly IAdapterSettings _settings;

        public AdapterContext(IAdapterSettings settings)
        {
            _settings = settings;
        }

        private IXmlToTableAdapter _databaseAdapter;
        private IXmlToTableAdapter Adapter
        {
            get
            {
                if (_databaseAdapter == null)
                {
                    if (_settings.IsHierarchicalModel)
                    {
                        _databaseAdapter = new HierarchicalModel(_settings);
                    }
                    else
                    {
                        _databaseAdapter = new KeyValueModel();
                    }
                }
                return _databaseAdapter;
            }
            set
            {
                _databaseAdapter = value;
            }
        }

        public string GenerateDatabaseCreationScript()
        {
            StringBuilder creationScript = new StringBuilder();
            creationScript.AppendLine(SqlBuilder.BuildCreateDatabaseStatement(_settings.RepositoryName));
            creationScript.AppendLine(SqlServer.DefaultBatchSeparator).AppendLine();
            creationScript.AppendLine(SqlExtensionMethods.BuildUseStatement(_settings.RepositoryName));
            creationScript.AppendLine(SqlServer.DefaultBatchSeparator).AppendLine();
            creationScript.AppendLine(Adapter.DatabaseCreationScript);
            return creationScript.ToString();
        }

        public string DatabaseCreationScript
        {
            get { return Adapter.DatabaseCreationScript; }
        }

        public void Initialize(SqlConnection repositoryConnection)
        {
            Adapter.Initialize(repositoryConnection);
        }

        public void ImportDocument(int documentId, string providerName, XmlDocument content)
        {
            Adapter.ImportDocument(documentId, providerName, content);
        }

        public void SaveChanges(SqlTransactionExtended transaction)
        {
            Adapter.SaveChanges(transaction);
        }

        public void Reset()
        {
            Adapter = null;
        }
    }
}