using System.Collections.Generic;
using System.Data;

namespace XmlToTable.Core
{
    public class Schema
    {
        public Schema()
        {
            Tables = new List<DataTable>();
        }

        public string Name { get; set; }
        public bool Created { get; set; }
        public List<DataTable> Tables { get; private set; }
    }
}