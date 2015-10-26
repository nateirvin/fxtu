namespace System.Data.SqlClient
{
    public static class SqlServer
    {
        public const string DefaultSchemaName = "dbo";

        // ReSharper disable InconsistentNaming
        public const string sp_AddExtendedProperty = "sys.sp_addextendedproperty";
        // ReSharper restore InconsistentNaming

        public const string DefaultBatchSeparator = "GO";
        public static string BeginTransactionStatement = "SET XACT_ABORT ON;\nBEGIN TRANSACTION;";
        public static string CommitTransactionStatement = "COMMIT TRANSACTION;";
    }
}