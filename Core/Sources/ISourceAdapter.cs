using System;
using System.Collections.Generic;
using System.Data;

namespace XmlToTable.Core.Sources
{
    internal interface ISourceAdapter : IDisposable
    {
        void OpenConnection();
        DocumentModel.MetaDataDataTable GetDocumentMetaData();
        DataTable GetPriorityItems();
        IDataReader GetDocumentBatchReader(List<string> documentIds);
    }
}