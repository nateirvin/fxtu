using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace System.Data
{
    public static class SqlExtensionMethods
    {
        public static bool IsSelectQuery(this string sourceSpecification)
        {
            return Regex.IsMatch(sourceSpecification, "SELECT.+?FROM", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        public static void SwitchDatabaseContext(this IDbConnection connection, string databaseName)
        {
            connection.ExecuteStatement(BuildUseStatement(databaseName));
        }

        public static void ExecuteStatements(this IDbConnection connection, string sql, string batchSeparator = SqlServer.DefaultBatchSeparator)
        {
            foreach (string statement in sql.ToSqlStatements(batchSeparator))
            {
                connection.ExecuteStatement(statement);
            }
        }

        public static string[] ToSqlStatements(this string sql, string batchSeparator = SqlServer.DefaultBatchSeparator)
        {
            return sql.Split(new[] { batchSeparator }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ExecuteStatement(this IDbConnection connection, string sql, params IDbDataParameter[] parameters)
        {
            ExecuteSql(connection, sql, parameters, CommandType.Text);
        }

        public static void ExecuteProcedure(this IDbConnection connection, string procedureName, params IDbDataParameter[] parameters)
        {
            ExecuteSql(connection, procedureName, parameters, CommandType.StoredProcedure);
        }

        private static void ExecuteSql(IDbConnection connection, string sql, IDbDataParameter[] parameters, CommandType commandType)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Connection = connection;
                command.CommandType = commandType;
                if (parameters != null)
                {
                    foreach (IDbDataParameter parameter in parameters)
                    {
                        command.Parameters.Add(parameter);
                    }
                }
                command.ExecuteNonQuery();
            }
        }

        public static T GetValue<T>(this PropertyCollection collection, object key)
        {
            return (T)collection[key];
        }

        public static string BuildUseStatement(string databaseName)
        {
            return String.Format("USE [{0}]", databaseName);
        }

        public static int GetInt32(this SqlConnection connection, string query, params SqlParameter[] parameters)
        {
            using (SqlCommand getCommand = new SqlCommand(query))
            {
                getCommand.Connection = connection;
                getCommand.CommandType = CommandType.Text;
                if (parameters != null)
                {
                    getCommand.Parameters.AddRange(parameters);
                }

                object rawValue = getCommand.ExecuteScalar();

                return (rawValue == DBNull.Value || rawValue == null) ? 0 : (int)rawValue;
            }
        }
    }
}