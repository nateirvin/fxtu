using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;

namespace XmlToTable.Core.Sources
{
    internal class NtfsSourceAdapter : ISourceAdapter
    {
        private DirectoryInfo _directoryInfo;

        public NtfsSourceAdapter(ShreddingEngineSettings settings)
        {
            Settings = settings;
        }

        private ShreddingEngineSettings Settings { get; }

        public void OpenConnection()
        {
            _directoryInfo = new DirectoryInfo(Settings.SourceLocation);
            if (!_directoryInfo.Exists)
            {
                throw new IOException("The source folder is not available.");
            }
        }

        public DocumentModel.MetaDataDataTable GetDocumentMetaData()
        {
            if (_directoryInfo == null)
            {
                OpenConnection();
            }

            DocumentModel.MetaDataDataTable documentsDataTable = new DocumentModel.MetaDataDataTable();

            FileInfo[] fileSystemObjects = _directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (FileInfo fileInfo in fileSystemObjects)
            {
                if (IsDocumentFile(fileInfo.FullName))
                {
                    string fileKey = fileInfo.FullName.Replace(_directoryInfo.FullName, String.Empty);
                    if (fileKey.StartsWith("/") || fileKey.StartsWith("\\"))
                    {
                        fileKey = fileKey.Substring(1);
                    }
                    documentsDataTable.AddMetaDataRow(fileKey, "unknown_provider", 0, fileInfo.CreationTime);
                }
            }

            return documentsDataTable;
        }

        private bool IsDocumentFile(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(Settings.SourceSpecification))
            {
                return Regex.IsMatch(filePath, Settings.SourceSpecification, RegexOptions.IgnoreCase);
            }

            return true;
        }

        public DataTable GetPriorityItems()
        {
            throw new NotSupportedException();
        }

        public IDataReader GetDocumentBatchReader(List<string> documentIds)
        {
            DocumentModel.ContentDataTable documentsDataTable = new DocumentModel.ContentDataTable();

            foreach (string documentId in documentIds)
            {
                string fullPath = Path.Combine(Settings.SourceLocation, documentId);
                string xml = File.ReadAllText(fullPath);
                documentsDataTable.AddContentRow(documentId, "unknown_provider", xml);
            }

            return new DataTableReader(documentsDataTable);
        }

        public void Dispose()
        {
            _directoryInfo = null;
        }
    }
}