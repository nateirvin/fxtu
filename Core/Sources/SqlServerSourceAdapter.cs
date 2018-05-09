using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace XmlToTable.Core.Sources
{
    internal class SqlServerSourceAdapter : ISourceAdapter
    {
        private SqlConnection _sourceConnection;
        private SqlCommand _getBatchCommand;
        private readonly ShreddingEngineSettings _settings;

        public SqlServerSourceAdapter(ShreddingEngineSettings settings)
        {
            _settings = settings;
        }

        public void OpenConnection()
        {
            if (_sourceConnection != null)
            {
                throw new InvalidOperationException("The connection has already been initialized.");
            }

            _sourceConnection = new SqlConnection(_settings.SourceLocation);
            _sourceConnection.Open();
        }

        public DocumentModel.MetaDataDataTable GetDocumentMetaData()
        {
            string sourceQuery = SqlBuilder.BuildGetAllDocumentsInfoQuery(_settings.SourceSpecification);
            DataSet documentsDataContainer = _sourceConnection.GetDataSetFromQuery(sourceQuery, commandTimeout: _settings.SourceQueryTimeout);
            return new DocumentModel.MetaDataDataTable(documentsDataContainer.Tables[0]);
        }

        public DocumentModel.IdListDataTable GetPriorityItems()
        {
            string query = SqlBuilder.BuildGetPriorityItemsQuery(_settings.SourceSpecification);
            List<SqlParameter> parameters = new List<SqlParameter> { new SqlParameter("@ProviderName", _settings.ProviderToProcess) };
            DataSet priorityItemsContainer = _sourceConnection.GetDataSetFromQuery(query, parameters, _settings.SourceQueryTimeout);
            return new DocumentModel.IdListDataTable(priorityItemsContainer.Tables[0]);
        }

        public IDataReader GetDocumentBatchReader(List<string> documentIds)
        {
            DisposeCurrentBatchRead();

            _getBatchCommand = new SqlCommand(SqlBuilder.BuildGetBatchItemsQuery(_settings.SourceSpecification, documentIds));
            _getBatchCommand.Connection = _sourceConnection;
            _getBatchCommand.CommandTimeout = _settings.SourceQueryTimeout;

            return _getBatchCommand.ExecuteReader();
        }

        public void Dispose()
        {
            DisposeCurrentBatchRead();
            _sourceConnection?.Dispose();
            _sourceConnection = null;
        }

        private void DisposeCurrentBatchRead()
        {
            _getBatchCommand?.Dispose();
        }
    }
}