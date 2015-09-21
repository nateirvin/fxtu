using System;
using System.Collections.Generic;
using System.Data;

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

        private static string BuildFromClause(string sourceSpecification)
        {
            return sourceSpecification.IsSelectQuery() ? String.Format("({0}) AS src", sourceSpecification) : sourceSpecification;
        }
    }
}