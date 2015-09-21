namespace XmlToTable.Core
{
    public static class SqlStatements
    {
        // ReSharper disable InconsistentNaming
        public const string usp_GetExtendedProperties = "usp_GetExtendedProperties";
        public const string usp_ImportDocumentInfos = "dbo.usp_ImportDocumentInfos";
        public const string usp_SetPriority = "dbo.usp_SetPriority";
        public const string usp_GetTablesAndColumns = "dbo.usp_GetTablesAndColumns";
        public const string usp_GetAllVariables = "dbo.usp_GetAllVariables";
        public const string usp_GetBatchToProcess = "dbo.usp_GetBatchToProcess";
        public const string usp_InsertVariables = "dbo.usp_InsertVariables";
        public const string usp_UpdateDataKinds = "dbo.usp_UpdateDataKinds";
        public const string usp_MarkProcessed = "dbo.usp_MarkProcessed";
        // ReSharper restore InconsistentNaming

        public static string GetDatabaseId
        {
            get { return "SELECT database_id FROM sys.databases WHERE name = @DatabaseName"; }
        }

        public static string GetObjectId
        {
            get { return "SELECT OBJECT_ID(@ObjectName);"; }
        }
    }
}