using System;
using System.Collections;
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
            return new FileByFileReader(Settings.SourceLocation, documentIds).ToEnumerable();
        }

        private class FileByFileReader : IEnumerator<DocumentContent>
        {
            private readonly IEnumerator<string> _idListEnumerator;

            public FileByFileReader(string folderRoot, IEnumerable<string> documentIds)
            {
                FolderRoot = folderRoot;
                _idListEnumerator = documentIds.GetEnumerator();
            }

            private string FolderRoot { get; }

            public bool MoveNext()
            {
                return _idListEnumerator.MoveNext();
            }

            public void Reset()
            {
                _idListEnumerator.Reset();
            }

            public DocumentContent Current
            {
                get
                {
                    if (_idListEnumerator.Current == null)
                    {
                        return null;
                    }

                    string documentId = _idListEnumerator.Current;
                    string fullPath = Path.Combine(FolderRoot, documentId);
                    string xml = File.ReadAllText(fullPath);
                    object xmlDoc = XmlParser.TryParseXml(xml);
                    return new DocumentContent
                    {
                        DocumentID = documentId,
                        ProviderName = DefaultProviderName,
                        Content = xmlDoc
                    };
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _idListEnumerator.Dispose();
            }
        }

        public void Dispose()
        {
            _directoryInfo = null;
        }
    }
}