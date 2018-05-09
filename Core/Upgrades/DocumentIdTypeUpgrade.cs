using System.Data;
using System.Data.SqlClient;
using XmlToTable.Core.Properties;

namespace XmlToTable.Core.Upgrades
{
    public class DocumentIdTypeUpgrade : IUpgrade
    {
        private DocumentIdTypeUpgrade(bool isKeyValueModel)
        {
            IsKeyValueModel = isKeyValueModel;
        }

        private bool IsKeyValueModel { get; }

        public bool IsRequired(SqlConnection repositoryConnection)
        {
            string typeName = repositoryConnection.GetString(
                @"SELECT [types].[name]
                FROM sys.tables
                    INNER JOIN sys.columns
                        ON tables.object_id = columns.object_id
                    INNER JOIN sys.types
                        ON columns.system_type_id = types.system_type_id
                WHERE tables.[name] = 'DocumentInfos'
                    AND columns.[name] = 'DocumentID'");
            return typeName.Equals("int");
        }

        public string DatabaseScript
        {
            get
            {
                if (IsKeyValueModel)
                {
                    return Resources.KeyValueDocumentIdUpgrade;
                }

                return Resources.HierarchicalDocumentIdUpgrade;
            }
        }

        public static DocumentIdTypeUpgrade GetHierarchicalModelUpgrade()
        {
            return new DocumentIdTypeUpgrade(isKeyValueModel: false);
        }

        public static DocumentIdTypeUpgrade GetKeyValueModelUpgrade()
        {
            return new DocumentIdTypeUpgrade(isKeyValueModel: true);
        }
    }
}