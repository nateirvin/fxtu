using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace XmlToTable.Core.Sources
{
    internal class SqlServerSourceAdapter : ISourceAdapter
    {
        private SqlConnection _sourceConnection;
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

        public IEnumerable<DocumentContent> GetContent(IEnumerable<string> documentIds)
        {
            string query = SqlBuilder.BuildGetBatchItemsQuery(_settings.SourceSpecification, documentIds.ToList());
            SqlCommand getBatchCommand = new SqlCommand(query);
            getBatchCommand.Connection = _sourceConnection;
            getBatchCommand.CommandTimeout = _settings.SourceQueryTimeout;
            SqlDataReader reader = getBatchCommand.ExecuteReader();
            return new SqlDataEnumerable(reader).ToEnumerable();
        }

        private class SqlDataEnumerable : IEnumerator<DocumentContent>
        {
            private readonly SqlDataReader _reader;

            public SqlDataEnumerable(SqlDataReader reader)
            {
                _reader = reader;
            }

            public bool MoveNext()
            {
                return _reader.Read();
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            object IEnumerator.Current => Current;

            public DocumentContent Current
            {
                get
                {
                    DocumentContent documentContent = new DocumentContent();
                    documentContent.DocumentID = _reader[Columns.DocumentId].ToString();
                    documentContent.ProviderName = _reader[Columns.ProviderName].ToString();
                    documentContent.Xml = _reader[Columns.Xml].ToString();
                    return documentContent;
                }
            }

            public void Dispose()
            {
                _reader.Dispose();
            }
        }

        public void Dispose()
        {
            _sourceConnection?.Dispose();
            _sourceConnection = null;
        }
    }
}