using System;
using System.Data;

namespace XmlToTable.Core
{
    public interface IDataTypeHelper
    {
        SqlDbType? SuggestType(SqlDbType? currentType, string newValue);
        object ConvertTo(Type destinationType, string rawValue);
    }
}