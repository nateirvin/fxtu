using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml;
using XmlToTable.Core.Properties;

namespace XmlToTable.Core
{
    public class ShreddingEngine : IDisposable
    {
        private readonly ShreddingEngineSettings _settings;
        private readonly SqlConnection _repositoryConnection;
        private readonly SqlConnection _sourceConnection;
        private readonly AdapterContext _adapterContext;

        public ShreddingEngine()
            : this(new ShreddingEngineSettings())
        {
        }

        public ShreddingEngine(ShreddingEngineSettings settings)
        {
            _settings = settings;
            EngineState = ComponentState.Uninitialized;
            _sourceConnection = new SqlConnection(_settings.SourceConnectionAddress);
            _repositoryConnection = new SqlConnection(_settings.GetRepositoryConnectionAddress());
            _adapterContext = new AdapterContext(_settings);
        }

        internal ComponentState EngineState { get; private set; }
        public event ProgressChangedEventHandler OnProgressChanged;

        public void Initialize()
        {
            if (EngineState != ComponentState.Uninitialized)
            {
                throw new InvalidOperationException("This object has already been initialized or disposed.");
            }

            _sourceConnection.Open();
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
            string repositoryName = _settings.RepositoryName;
            int databaseId = _repositoryConnection.GetInt32(SqlStatements.GetDatabaseId, new SqlParameter("@DatabaseName", repositoryName));

            if (databaseId == 0)
            {
                ShowProgress(0, "Creating database");
                _adapterContext.CreateDatabase(_repositoryConnection);
            }

            _repositoryConnection.SwitchDatabaseContext(repositoryName);

            if (_adapterContext.RequiresUpgrade(_repositoryConnection))
            {
                ShowProgress(0, "Upgrading database");
                _adapterContext.UpgradeDatabase(_repositoryConnection);
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

                string sourceQuery = SqlBuilder.BuildGetAllDocumentsInfoQuery(_settings.SourceSpecification);
                DataSet documentsDataContainer = _sourceConnection.GetDataSetFromQuery(sourceQuery, commandTimeout: _settings.SourceQueryTimeout);

                _repositoryConnection.ExecuteProcedure(SqlStatements.usp_ImportDocumentInfos, new SqlParameter("@Items", documentsDataContainer.Tables[0]));
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
                    string query = SqlBuilder.BuildGetPriorityItemsQuery(_settings.SourceSpecification);
                    List<SqlParameter> parameters = new List<SqlParameter> { new SqlParameter("@ProviderName", _settings.ProviderToProcess)};
                    DataSet priorityItemsContainer = _sourceConnection.GetDataSetFromQuery(query, parameters, _settings.SourceQueryTimeout);
                    priorityItemsTable = priorityItemsContainer.Tables[0];
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

                List<int> documentIds = GetIdsToProcess(batchSize);
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

        private List<int> GetIdsToProcess(int batchSize)
        {
            List<int> idsToProcess = new List<int>();

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
                        idsToProcess.Add((int) batchIdReader[Columns.DocumentId]);
                    }
                }
            }

            return idsToProcess;
        }

        private void Import(List<int> documentIds)
        {
            int processedCount = 0;
            using (SqlCommand getBatchCommand = new SqlCommand(SqlBuilder.BuildGetBatchItemsQuery(_settings.SourceSpecification, documentIds)))
            {
                getBatchCommand.Connection = _sourceConnection;
                getBatchCommand.CommandTimeout = _settings.SourceQueryTimeout;
                using (SqlDataReader batchItemReader = getBatchCommand.ExecuteReader())
                {
                    while (batchItemReader.Read())
                    {
                        processedCount++;
                        int progressValue = Math.Max(1, (int) ((processedCount / (decimal)documentIds.Count))*100);
                        if (progressValue == 100 && processedCount != documentIds.Count)
                        {
                            progressValue = 99;
                        }
                        string message = string.Format("Importing {0} of {1}", processedCount, documentIds.Count);
                        ShowProgress(progressValue, message);

                        int documentID = Convert.ToInt32(batchItemReader[Columns.DocumentId]);
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
        }

        private void CommitChanges(List<int> documentIds)
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

        private void UpdateProcessedItems(SqlTransaction transaction, List<int> processedDocumentIds)
        {
            DataTable parameterValue = BuildIdsDataTable();
            foreach (int id in processedDocumentIds)
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
            table.Columns.Add("ID", typeof(int));
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
            _sourceConnection.Dispose();
        }
    }
}