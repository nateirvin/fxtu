using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace XmlToTable.Core
{
    public static class ExtensionMethods
    {
        public static string ToSqlName(this string name)
        {
            return name.Replace("#", String.Empty);
        }

        internal static DataSet GetDataSetFromQuery(this SqlConnection connection, string commandText, List<SqlParameter> parameters = null, int commandTimeout = 30)
        {
            return GetDataSet(connection, commandText, CommandType.Text, parameters, commandTimeout);
        }

        internal static DataSet GetDataSetFromProcedure(this SqlConnection connection, string procedureName, List<SqlParameter> parameters = null, int commandTimeout = 30)
        {
            return GetDataSet(connection, procedureName, CommandType.StoredProcedure, parameters, commandTimeout);
        }

        private static DataSet GetDataSet(SqlConnection connection, string commandText, CommandType commandType, List<SqlParameter> parameters, int commandTimeout)
        {
            DataSet dataSet = new DataSet();

            using (SqlCommand command = new SqlCommand(commandText))
            {
                command.Connection = connection;
                command.CommandType = commandType;
                command.CommandTimeout = commandTimeout;
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters.ToArray());
                }

                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    adapter.Fill(dataSet);
                }
            }

            return dataSet;
        }

        internal static bool CanBe<T>(this object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            Parser parser = Parser.GetParser<T>();
            if (parser != null)
            {
                return parser.CanParse(value.ToString());
            }

            return CheckTypeSlowly<T>(value);
        }

        private static bool CheckTypeSlowly<T>(object value)
        {
            TypeConverter typeConverter = TypeDescriptor.GetConverter(typeof (T));

            if (typeConverter == null)
            {
                throw new NotSupportedException();
            }

            bool canConvert = typeConverter.CanConvertFrom(value.GetType());
            if (canConvert)
            {
                try
                {
                    typeConverter.ConvertFrom(value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public static bool IsOneOf<T>(this T actualValue, params T[] possibleValues)
        {
            if (possibleValues.Length == 0)
            {
                throw new ArgumentException();
            }
            return possibleValues.ToList().Contains(actualValue);
        }

        internal static Dictionary<string, string> ToDictionary(this Hashtable hashtable, bool makeKeysLowercase = false)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            foreach (DictionaryEntry entry in hashtable)
            {
                if (entry.Key != null)
                {
                    string key = entry.Key.ToString();
                    if (makeKeysLowercase)
                    {
                        key = key.ToLower();
                    }
                    string value = entry.Value == null ? null : entry.Value.ToString();
                    dictionary.Add(key, value);
                }
            }

            return dictionary;
        }

        internal static string ToNullPreferredString(this string value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}