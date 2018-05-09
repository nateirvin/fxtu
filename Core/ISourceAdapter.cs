using System;
using System.Collections.Generic;
using System.Data;

namespace XmlToTable.Core
{
    internal interface ISourceAdapter : IDisposable
    {
        void OpenConnection();
        DataTable GetDocumentInfos();
        DataTable GetPriorityItems();
        IDataReader GetDocumentBatchReader(List<string> documentIds);
    }
}