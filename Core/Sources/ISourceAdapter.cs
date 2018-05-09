using System;
using System.Collections.Generic;

namespace XmlToTable.Core.Sources
{
    internal interface ISourceAdapter : IDisposable
    {
        void OpenConnection();
        DocumentModel.MetaDataDataTable GetDocumentMetaData();
        DocumentModel.IdListDataTable GetPriorityItems();
        IEnumerable<DocumentContent> GetContent(IEnumerable<string> documentIds);
    }
}