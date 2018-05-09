using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;

namespace XmlToTable.Core
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
            _directoryInfo = new DirectoryInfo(Settings.SourceConnectionAddress);
            if (!_directoryInfo.Exists)
            {
                throw new IOException("The source folder is not available.");
            }
        }

        public DocumentInfo.DocumentsDataTable GetDocumentInfos()
        {
            if (_directoryInfo == null)
            {
                OpenConnection();
            }

            DocumentInfo.DocumentsDataTable documentsDataTable = new DocumentInfo.DocumentsDataTable();

            FileInfo[] fileInfos = _directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (IsDocumentFile(fileInfo.FullName))
                {
                    string fileKey = fileInfo.FullName.Replace(_directoryInfo.FullName, String.Empty);
                    if (fileKey.StartsWith("/") || fileKey.StartsWith("\\"))
                    {
                        fileKey = fileKey.Substring(1);
                    }
                    documentsDataTable.AddDocumentsRow(fileKey, null, 0, fileInfo.CreationTime);
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
            DocumentInfo.DocumentContentDataTable documentsDataTable = new DocumentInfo.DocumentContentDataTable();

            foreach (string documentId in documentIds)
            {
                string fullPath = Path.Combine(Settings.SourceConnectionAddress, documentId);
                string xml = File.ReadAllText(fullPath);
                documentsDataTable.AddDocumentContentRow(documentId, null, xml);
            }

            return new DataTableReader(documentsDataTable);
        }

        public void Dispose()
        {
            _directoryInfo = null;
        }
    }
}