using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

namespace XmlToTable.Core
{
    public static class SqlBuilder
    {
        public static string BuildCreateDatabaseStatement(string databaseName)
        {
            return String.Format("CREATE DATABASE [{0}]", databaseName);
        }

        public static string BuildGetAllDocumentsInfoQuery(string sourceSpecification)
        {
            return String.Format("SELECT DocumentID, ProviderName, SubjectID, GenerationDate FROM {0}", BuildFromClause(sourceSpecification));
        }

        public static string BuildGetPriorityItemsQuery(string sourceSpecification)
        {
            return String.Format("SELECT DocumentID AS ID FROM {0} WHERE ProviderName = @ProviderName", BuildFromClause(sourceSpecification));
        }

        public static string BuildGetBatchItemsQuery(string sourceSpecification, List<int> idsToProcess)
        {
            string whereClause = String.Format("DocumentID IN ({0})", String.Join(",", idsToProcess));
            return String.Format("SELECT DocumentID, ProviderName, [XML] FROM {0} WHERE {1}", BuildFromClause(sourceSpecification), whereClause);
        }

        internal static string BuildFromClause(string sourceSpecification)
        {
            if (sourceSpecification.IsSelectQuery())
            {
                return String.Format("({0}) AS src", sourceSpecification);
            }

            sourceSpecification = sourceSpecification.Trim();

            bool inBracket = false;
            List<string> names = new List<string>();
            StringBuilder buffer = new StringBuilder();
            foreach (char thisChar in sourceSpecification)
            {
                if (thisChar == '[')
                {
                    inBracket = true;
                }
                else if (thisChar == ']')
                {
                    inBracket = false;
                }
                else if (thisChar == '.' && !inBracket)
                {
                    names.Add(string.Format("[{0}]", buffer.Length == 0 ? SqlServer.DefaultSchemaName : buffer.ToString()));
                    buffer.Clear();
                }
                else
                {
                    buffer.Append(thisChar);
                }
            }
            if (buffer.Length > 0)
            {
                names.Add(string.Format("[{0}]", buffer));
            }
            if (names.Count == 1)
            {
                names.Insert(0, string.Format("[{0}]", SqlServer.DefaultSchemaName));
            }
            return string.Join(".", names);
        }
    }
}