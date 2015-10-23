using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml;
using XmlToTable.Core.Upgrades;

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
            creationScript.AppendLine(DatabaseCreationScript);
            return creationScript.ToString();
        }

        public void CreateDatabase(SqlConnection repositoryConnection)
        {
            ExecuteObjectTransaction(repositoryConnection, DatabaseCreationScript, 15);
        }

        public string DatabaseCreationScript
        {
            get { return Adapter.DatabaseCreationScript; }
        }

        public bool RequiresUpgrade(SqlConnection repositoryConnection)
        {
            return Upgrades.Any(upgrade => upgrade.IsRequired(repositoryConnection));
        }

        public string GenerateDatabaseUpgradeScript()
        {
            StringBuilder script = new StringBuilder();
            
            if (Upgrades.Any())
            {
                script.AppendLine(SqlExtensionMethods.BuildUseStatement(_settings.RepositoryName));
                script.AppendLine(SqlServer.DefaultBatchSeparator).AppendLine();
                script.AppendLine(SqlServer.BeginTransactionStatement);
                script.AppendLine(SqlServer.DefaultBatchSeparator).AppendLine();
                foreach (IUpgrade upgrade in Upgrades)
                {
                    script.AppendLine(upgrade.DatabaseScript);
                }
                script.AppendLine(SqlServer.DefaultBatchSeparator).AppendLine();
                script.AppendLine(SqlServer.CommitTransactionStatement);
                script.AppendLine(SqlServer.DefaultBatchSeparator).AppendLine();
            }

            return script.ToString();
        }

        public void UpgradeDatabase(SqlConnection repositoryConnection)
        {
            StringBuilder batch = new StringBuilder();
            foreach (IUpgrade upgrade in Upgrades)
            {
                if (upgrade.IsRequired(repositoryConnection))
                {
                    batch.AppendLine(upgrade.DatabaseScript);
                    batch.AppendLine(SqlServer.DefaultBatchSeparator);
                }
            }

            if (batch.Length > 0)
            {
                ExecuteObjectTransaction(repositoryConnection, batch.ToString(), 300);
            }
        }

        public IEnumerable<IUpgrade> Upgrades
        {
            get { return Adapter.Upgrades; }
        }

        public void Initialize(SqlConnection repositoryConnection)
        {
            Adapter.Initialize(repositoryConnection);
        }

        private static void ExecuteObjectTransaction(SqlConnection repositoryConnection, string script, int timeout)
        {
            SqlTransaction transaction = null;
            try
            {
                transaction = repositoryConnection.BeginTransaction();

                foreach (string statement in script.ToSqlStatements())
                {
                    using (SqlCommand objectModificationStatement = new SqlCommand(statement))
                    {
                        objectModificationStatement.Connection = repositoryConnection;
                        objectModificationStatement.Transaction = transaction;
                        objectModificationStatement.CommandType = CommandType.Text;
                        objectModificationStatement.CommandTimeout = timeout;
                        objectModificationStatement.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            finally
            {
                if (transaction != null)
                {
                    transaction.Dispose();
                }
            }
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