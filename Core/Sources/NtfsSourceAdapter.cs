using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace XmlToTable.Core.Sources
{
    internal class NtfsSourceAdapter : ISourceAdapter
    {
        private const string DefaultProviderName = "unknown_provider";
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
                    documentsDataTable.AddMetaDataRow(fileKey, DefaultProviderName, 0, fileInfo.CreationTime);
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

        public DocumentModel.IdListDataTable GetPriorityItems()
        {
            throw new NotSupportedException();
        }

        public IEnumerable<DocumentContent> GetContent(IEnumerable<string> documentIds)
        {
            List<DocumentContent> contentRecords = new List<DocumentContent>();

            foreach (string documentId in documentIds)
            {
                string fullPath = Path.Combine(Settings.SourceLocation, documentId);
                string xml = File.ReadAllText(fullPath);
                contentRecords.Add(new DocumentContent
                {
                    DocumentID = documentId,
                    ProviderName = DefaultProviderName,
                    Xml = xml
                });
            }

            return contentRecords;
        }

        public void Dispose()
        {
            _directoryInfo = null;
        }
    }
}