using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using XmlToTable.Core.Properties;
using XmlToTable.Core.Upgrades;

namespace XmlToTable.Core
{
    public class ShreddingEngine : IDisposable
    {
        private readonly ShreddingEngineSettings _settings;
        private readonly SqlConnection _repositoryConnection;
        private readonly ISourceAdapter _sourceAdapter;
        private readonly AdapterContext _adapterContext;

        public ShreddingEngine()
            : this(new ShreddingEngineSettings())
        {
        }

        public ShreddingEngine(ShreddingEngineSettings settings)
        {
            _settings = settings;
            EngineState = ComponentState.Uninitialized;
            _repositoryConnection = new SqlConnection(_settings.GetRepositoryConnectionAddress());
            _adapterContext = new AdapterContext(_settings);

            if (_settings.IsFileSource)
            {
                _sourceAdapter = new NtfsSourceAdapter(_settings);
            }
            else
            {
                _sourceAdapter = new SqlServerSourceAdapter(_settings);
            }
        }

        internal ComponentState EngineState { get; private set; }
        public event ProgressChangedEventHandler OnProgressChanged;

        public void Initialize()
        {
            if (EngineState != ComponentState.Uninitialized)
            {
                throw new InvalidOperationException("This object has already been initialized or disposed.");
            }

            _sourceAdapter.OpenConnection();
            _repositoryConnection.Open();

            CreateOrUpgradeRepositoryIfNecessary();
            ValidateSettings();
            UpdateImportList();
            SetPriorityProcessing();

            _adapterContext.Initialize(_repositoryConnection);
            EngineState = ComponentState.Ready;
        }

        private void CreateOrUpgradeRepositoryIfNecessary()
        {
            int databaseId = _settings.GetRepositoryID(_repositoryConnection);

            if (databaseId == 0)
            {
                ShowProgress(0, "Creating database");
                CreateDatabase(_repositoryConnection);
            }

            _repositoryConnection.SwitchDatabaseContext(_settings.RepositoryName);

            if (_adapterContext.RequiresUpgrade(_repositoryConnection))
            {
                ShowProgress(0, "Upgrading database");
                UpgradeDatabase(_repositoryConnection);
            }
        }
        
        private void CreateDatabase(SqlConnection repositoryConnection)
        {
            repositoryConnection.ExecuteStatement(SqlBuilder.BuildCreateDatabaseStatement(_settings.RepositoryName));
            repositoryConnection.SwitchDatabaseContext(_settings.RepositoryName);
            ExecuteObjectTransaction(repositoryConnection, _adapterContext.DatabaseCreationScript, 15);
        }

        private void UpgradeDatabase(SqlConnection repositoryConnection)
        {
            StringBuilder batch = new StringBuilder();
            foreach (IUpgrade upgrade in _adapterContext.Upgrades)
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

        private void ExecuteObjectTransaction(SqlConnection repositoryConnection, string script, int timeout)
        {
            SqlTransaction transaction = null;
            try
            {
                string[] statements = script.ToSqlStatements();

                transaction = repositoryConnection.BeginTransaction();

                for (int i = 0; i < statements.Length; i++)
                {
                    string statement = statements[i];
                    ShowItemProgress("Executing action", i + 1, statements.Length);

                    if (!string.IsNullOrWhiteSpace(statement))
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

        private void ValidateSettings()
        {
            int objectId = _repositoryConnection.GetInt32(SqlStatements.GetObjectId, new SqlParameter("@ObjectName", "dbo.Variables"));
            bool isKeyValueModel = objectId != 0;

            if (_settings.IsHierarchicalModel)
            {
                if (isKeyValueModel)
                {
                    throw new Exception("A hierarchical model cannot be changed to a key-value model.");
                }

                Dictionary<string, string> extendedProperties = GetExtendedProperties();
                ThrowIfPropertyAndSettingDoNotMatch(extendedProperties, HierarchicalModel.UseForeignKeysPropertyName, _settings.UseForeignKeys);
                ThrowIfPropertyAndSettingDoNotMatch(extendedProperties, HierarchicalModel.MaxNameLengthPropertyName, _settings.MaximumNameLength);
            }
            else
            {
                if (!isKeyValueModel)
                {
                    throw new Exception("A key-value model cannot be changed to a hierarchical model.");
                }
            }
        }

        private Dictionary<string, string> GetExtendedProperties()
        {
            Dictionary<string, string> extendedProperties = new Dictionary<string, string>();

            using (SqlCommand command = new SqlCommand(SqlStatements.usp_GetExtendedProperties))
            {
                command.Connection = _repositoryConnection;
                command.CommandType = CommandType.StoredProcedure;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        extendedProperties.Add(reader["name"].ToString(), reader["value"].ToString());
                    }
                }
            }

            return extendedProperties;
        }

        private static void ThrowIfPropertyAndSettingDoNotMatch<T>(Dictionary<string, string> extendedProperties, string propertyName, T setting)
        {
            string propertyValue = extendedProperties[propertyName];
            string settingValue = setting.ToString();

            if (!propertyValue.Equals(settingValue, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new Exception(string.Format("The '{0}' setting cannot be changed once the database has been created.", propertyName));
            }
        }

        private void UpdateImportList()
        {
            bool shouldImport = false;
            if (Debugger.IsAttached)
            {
                DialogResult response = MessageBox.Show("Do you want to import documents at this time?", Resources.ProgramName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (response == DialogResult.Yes)
                {
                    shouldImport = true;
                }
            }
            else
            {
                shouldImport = true;
            }

            if (shouldImport)
            {
                ShowProgress(0, "Gathering documents");
                DataTable documentInfos = _sourceAdapter.GetDocumentInfos();
                SqlParameter tableParameter = new SqlParameter("@Items", SqlDbType.Structured) {Value = documentInfos};
                _repositoryConnection.ExecuteProcedure(SqlStatements.usp_ImportDocumentInfos, tableParameter);
            }
        }

        private void SetPriorityProcessing()
        {
            ShowProgress(0, "Setting processing priority");

            if (_settings.IsHierarchicalModel)
            {
                DataTable priorityItemsTable;
                if (!string.IsNullOrWhiteSpace(_settings.ProviderToProcess))
                {
                    priorityItemsTable = _sourceAdapter.GetPriorityItems();
                }
                else
                {
                    priorityItemsTable = BuildIdsDataTable();
                }

                _repositoryConnection.ExecuteProcedure(SqlStatements.usp_SetPriority, new SqlParameter("@Items", priorityItemsTable));
            }
            else
            {
                object parameterValue = !string.IsNullOrWhiteSpace(_settings.ProviderToProcess) ? _settings.ProviderToProcess : (object)DBNull.Value;
                _repositoryConnection.ExecuteProcedure(SqlStatements.usp_SetPriority, new SqlParameter("@ProviderName", parameterValue));
            }
        }

        public int Shred(int batchSize)
        {
            if (EngineState == ComponentState.Uninitialized)
            {
                Initialize();
            }
            if (EngineState == ComponentState.Disposed)
            {
                throw new InvalidOperationException("This object has already been disposed.");
            }

            try
            {
                _adapterContext.Initialize(_repositoryConnection);

                List<string> documentIds = GetIdsToProcess(batchSize);
                if (documentIds.Count > 0)
                {
                    Import(documentIds);
                    CommitChanges(documentIds);
                }
                return documentIds.Count;
            }
            catch
            {
                _adapterContext.Reset();
                throw;
            }
        }

        private List<string> GetIdsToProcess(int batchSize)
        {
            List<string> idsToProcess = new List<string>();

            ShowProgress(0, "Retrieving processing batch");
            using (SqlCommand getBatchIdsCommand = new SqlCommand(SqlStatements.usp_GetBatchToProcess))
            {
                getBatchIdsCommand.Connection = _repositoryConnection;
                getBatchIdsCommand.CommandType = CommandType.StoredProcedure;
                getBatchIdsCommand.Parameters.AddWithValue("@BatchSize", batchSize);
                using (SqlDataReader batchIdReader = getBatchIdsCommand.ExecuteReader())
                {
                    while (batchIdReader.Read())
                    {
                        idsToProcess.Add(batchIdReader[Columns.DocumentId].ToString());
                    }
                }
            }

            return idsToProcess;
        }

        private void Import(List<string> documentIds)
        {
            int processedCount = 0;
            using (IDataReader batchItemReader = _sourceAdapter.GetDocumentBatchReader(documentIds))
            {
                while (batchItemReader.Read())
                {
                    processedCount++;
                    ShowItemProgress("Importing", processedCount, documentIds.Count);

                    string documentID = batchItemReader[Columns.DocumentId].ToString();
                    string providerName = batchItemReader[Columns.ProviderName].ToString();
                    string xml = batchItemReader[Columns.Xml].ToString();

                    XmlDocument xmlDocument = null;
                    if (!string.IsNullOrWhiteSpace(xml))
                    {
                        try
                        {
                            xmlDocument = xml.ToXmlDocument();
                        }
                        catch (XmlException xmlException)
                        {
                            ShowProgress(0, string.Format("\nDocument {0} was malformed. ({1})", documentID, xmlException.Message));
                        }
                    }
                    if (xmlDocument != null)
                    {
                        try
                        {
                            _adapterContext.ImportDocument(documentID, providerName, xmlDocument);
                        }
                        catch (Exception processingException)
                        {
                            throw new InvalidOperationException(string.Format("Error processing item {0}: {1}", documentID, processingException.Message), processingException);
                        }
                    }
                }
            }
        }

        private void ShowItemProgress(string actionName, int itemNumber, int totalItems)
        {
            int progressPercentage = Math.Max(1, (int)((itemNumber / (decimal)totalItems)) * 100);
            if (progressPercentage == 100 && itemNumber != totalItems)
            {
                progressPercentage = 99;
            }
            
            string message = string.Format("{0} {1} of {2}", actionName, itemNumber, totalItems);

            ShowProgress(progressPercentage, message);
        }

        private void CommitChanges(List<string> documentIds)
        {
            ShowProgress(0, "Writing");

            using (SqlTransactionExtended transaction = new SqlTransactionExtended(_repositoryConnection.BeginTransaction()))
            {
                try
                {
                    _adapterContext.SaveChanges(transaction);
                    UpdateProcessedItems(transaction, documentIds);
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void UpdateProcessedItems(SqlTransaction transaction, List<string> processedDocumentIds)
        {
            DataTable parameterValue = BuildIdsDataTable();
            foreach (string id in processedDocumentIds)
            {
                DataRow row = parameterValue.NewRow();
                row["ID"] = id;
                parameterValue.Rows.Add(row);
            }

            using (SqlCommand markProcessedCommand = new SqlCommand(SqlStatements.usp_MarkProcessed))
            {
                markProcessedCommand.CommandType = CommandType.StoredProcedure;
                markProcessedCommand.Connection = transaction.Connection;
                markProcessedCommand.Transaction = transaction;
                markProcessedCommand.Parameters.AddWithValue("@Items", parameterValue);
                markProcessedCommand.ExecuteNonQuery();
            }
        }

        private static DataTable BuildIdsDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("ID", typeof(string));
            return table;
        }

        private void ShowProgress(int progress, string message)
        {
            if (OnProgressChanged != null)
            {
                OnProgressChanged(this, new ProgressChangedEventArgs(progress, message));
            }
        }

        public void Dispose()
        {
            _repositoryConnection.Dispose();
            _sourceAdapter.Dispose();
        }
    }
}