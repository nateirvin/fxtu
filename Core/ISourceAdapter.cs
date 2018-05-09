using System;
using System.Collections.Generic;
using System.Data;

namespace XmlToTable.Core
{
    internal interface ISourceAdapter : IDisposable
    {
        void OpenConnection();
        DocumentInfo.DocumentsDataTable GetDocumentInfos();
        DataTable GetPriorityItems();
        IDataReader GetDocumentBatchReader(List<string> documentIds);
    }
}