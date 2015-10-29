using System;
using System.Data;
using System.Data.SqlClient;
using XmlToTable.Core.Properties;

namespace XmlToTable.Core.Upgrades
{
    public class EmbeddedXmlUpgrade : IUpgrade
    {
        private string _documentQuery;

        public EmbeddedXmlUpgrade(string documentQuery)
        {
            _documentQuery = documentQuery;
        }

        public bool IsRequired(SqlConnection repositoryConnection)
        {
            int objectId = repositoryConnection.GetInt32(SqlStatements.GetObjectId, new SqlParameter("@ObjectName", SqlStatements.usp_ReprocessDocuments));
            return objectId == 0;
        }

        public string DatabaseScript
        {
            get
            {
                if (!_documentQuery.IsSelectQuery())
                {
                    _documentQuery = null;
                }
                if (string.IsNullOrWhiteSpace(_documentQuery))
                {
                    throw new InvalidOperationException("Missing or invalid document query.");
                }
                return Resources.EmbeddedXmlUpgradeScript.Replace("/* QUERY */", _documentQuery);
            }
        }
    }
}