using System;
using System.ComponentModel;
using System.Data;
using System.Reflection;

namespace XmlToTable.Core
{
    public class DataTypeHelper : IDataTypeHelper
    {
        private static readonly string[] TrueStrings = { "1", "T", "TRUE", "YES" };
        private static readonly string[] FalseStrings = { "0", "F", "FALSE", "NO" };

        public SqlDbType? SuggestType(SqlDbType? currentType, string newValue)
        {
            if (!currentType.IsOneOf(SqlDbType.NVarChar, SqlDbType.VarChar, SqlDbType.Char, SqlDbType.NChar))
            {
                SqlDbType? suggestedType = SuggestType(newValue);

                if (suggestedType.HasValue)
                {
                    if (currentType.HasValue)
                    {
                        if (currentType.Value == suggestedType.Value)
                        {
                            return currentType.Value;
                        }

                        return GetActualSuggestion(currentType.Value, suggestedType.Value);
                    }
                    return suggestedType.Value;
                }
            }

            return currentType;
        }

        private static SqlDbType? SuggestType(object value)
        {
            SqlDbType? suggestedType = null;

            if (value != DBNull.Value && value != null)
            {
                string upperValue = value.ToString().ToUpper();
                if (upperValue.IsOneOf(TrueStrings) || upperValue.IsOneOf(FalseStrings))
                {
                    suggestedType = SqlDbType.Bit;
                }
                else
                {
                    if (value.CanBe<Guid>())
                    {
                        suggestedType = SqlDbType.UniqueIdentifier;
                    }
                    else if (value.CanBe<decimal>())
                    {
                        suggestedType = SqlDbType.Float;
                        if (value.CanBe<long>())
                        {
                            suggestedType = SqlDbType.BigInt;
                        }
                        if (value.CanBe<int>())
                        {
                            suggestedType = SqlDbType.Int;
                        }
                    }
                    else if (value.CanBe<DateTime>())
                    {
                        suggestedType = SqlDbType.DateTime;
                    }
                    else
                    {
                        suggestedType = SqlDbType.NVarChar;
                    }
                }
            }

            return suggestedType;
        }

        private SqlDbType GetActualSuggestion(SqlDbType currentSuggestion, SqlDbType newSuggestion)
        {
            switch (newSuggestion)
            {
                case SqlDbType.BigInt:
                    if (currentSuggestion.IsOneOf(SqlDbType.Bit, SqlDbType.Int))
                    {
                        return newSuggestion;
                    }
                    if (currentSuggestion == SqlDbType.Float)
                    {
                        return currentSuggestion;
                    }
                    break;
                case SqlDbType.Int:
                    if (currentSuggestion.IsOneOf(SqlDbType.BigInt, SqlDbType.Float))
                    {
                        return currentSuggestion;
                    }
                    break;
            }

            return SqlDbType.NVarChar;
        }

        public object ConvertTo(Type destinationType, string rawValue)
        {
            if (rawValue == null)
            {
                return null;
            }

            if (destinationType == typeof (bool))
            {
                if (rawValue.ToUpper().IsOneOf(TrueStrings))
                {
                    return true;
                }
                if (rawValue.ToUpper().IsOneOf(FalseStrings))
                {
                    return false;
                }
            }
            else if (destinationType == typeof (string))
            {
                return rawValue.ToNullPreferredString();
            }
            else
            {
                MethodInfo methodInfo = destinationType.GetPublicStaticMethod("Parse");
                if (methodInfo != null)
                {
                    return methodInfo.Invoke(null, new object[] {rawValue});
                }
            }

            TypeConverter converter = TypeDescriptor.GetConverter(destinationType);
            return converter.ConvertTo(rawValue, destinationType);
        }
    }
}