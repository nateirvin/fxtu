using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml;
using XmlToTable.Core.Properties;
using XmlToTable.Core.Upgrades;

namespace XmlToTable.Core
{
    public class HierarchicalModel : IXmlToTableAdapter
    {
        private const int NvarcharBoundedThreshold = 4000;
        private const int MinimumColumnCharacters = 100;
        private const int WriteBatchSize = 1000;
        internal const string WriteStatePropertyName = "written";
        private const string HasContentPropertyName = "HasContent";
        internal const string UseForeignKeysPropertyName = "UseForeignKeys";
        internal const string MaxNameLengthPropertyName = "MaxNameLength";
        private const string DocumentsTableName = "DocumentInfos";
        private const string NameMappingTableName = "OriginalNames";
        private readonly List<string> _reservedColumns = new List<string> { Columns.DocumentId, Columns.Identity, Columns.ParentTableName, Columns.ParentID };

        private bool _initialized;
        private readonly IAdapterSettings _settings;
        private DataTable _documentsTable;
        private DataTable _namesTable;
        private readonly List<Schema> _schemas;
        private readonly Dictionary<DataTable, List<DataForeignKey>> _foreignKeys;

        private readonly NameHandler _nameHandler;
        private readonly IDataTypeHelper _dataTypeHelper;
        private readonly SqlDbTypeConverter _sqlDbTypeConverter;

        public HierarchicalModel(IAdapterSettings settings = null)
        {
            _initialized = false;
            _settings = settings ?? new AdapterSettings();
            _schemas = new List<Schema>();
            _foreignKeys = new Dictionary<DataTable, List<DataForeignKey>>();

            _nameHandler = new NameHandler();
            _dataTypeHelper = new DataTypeHelper();
            _sqlDbTypeConverter = new SqlDbTypeConverter();
        }

        public string DatabaseCreationScript
        {
            get
            {
                StringBuilder script = new StringBuilder();
                script.AppendLine(Resources.HierarchicalDatabaseCreationScript);
                script.AppendFormat("EXEC {0} @name='{1}', @value='{2}'", SqlServer.sp_AddExtendedProperty, UseForeignKeysPropertyName, _settings.UseForeignKeys).AppendLine();
                script.AppendFormat("EXEC {0} @name='{1}', @value='{2}'", SqlServer.sp_AddExtendedProperty, MaxNameLengthPropertyName, _settings.MaximumNameLength).AppendLine();
                return script.ToString();
            }
        }

        public IEnumerable<IUpgrade> Upgrades
        {
            get { return new List<IUpgrade>(); }
        }

        public void Initialize(SqlConnection repositoryConnection)
        {
            if (_initialized)
            {
                return;
            }

            DataSet metadataContainer = repositoryConnection.GetDataSetFromProcedure(SqlStatements.usp_GetTablesAndColumns);

            foreach (DataRow tableInfo in metadataContainer.Tables[0].Rows)
            {
                string schemaName = (string)tableInfo["SchemaName"];
                string tableName = (string)tableInfo["TableName"];

                Schema schema = EnsureSchema(schemaName);
                schema.Created = true;

                DataTable table = new DataTable();
                table.TableName = tableName;
                table.ExtendedProperties.Add(WriteStatePropertyName, PersistenceState.Written);
                table.ExtendedProperties.Add(HasContentPropertyName, true);
                _foreignKeys.Add(table, new List<DataForeignKey>());

                DataRow[] columnInfos = metadataContainer.Tables[1].Select(String.Format("object_id = {0}", tableInfo["object_id"]));
                foreach (DataRow columnInfo in columnInfos)
                {
                    DataColumn column = new DataColumn(columnInfo["ColumnName"].ToString());
                    column.ExtendedProperties.Add(WriteStatePropertyName, PersistenceState.Written);

                    SqlDbType dataType = (SqlDbType) _sqlDbTypeConverter.ConvertFrom(columnInfo["TypeName"].ToString().ToLower());
                    switch (dataType)
                    {
                        case SqlDbType.NVarChar:
                            column.DataType = typeof (string);
                            column.MaxLength = Convert.ToInt32(columnInfo["MaxCharacters"]);
                            break;
                        default:
                            Type type = (Type) _sqlDbTypeConverter.ConvertTo(dataType, typeof (Type));
                            column.DataType = type;
                            break;
                    }
                    column.AllowDBNull = (bool) columnInfo["is_nullable"];
                    if ((bool) columnInfo["is_identity"])
                    {
                        column.Unique = true;
                        column.AutoIncrement = true;
                        column.AutoIncrementSeed = Convert.ToInt32(columnInfo["last_value"]) + 1;
                        column.AutoIncrementStep = Convert.ToInt32(columnInfo["increment_value"]);
                    }
                    
                    table.Columns.Add(column);

                    if ((bool)columnInfo["IsPrimaryKey"])
                    {
                        if(table.PrimaryKey == null || table.PrimaryKey.Length == 0)
                        {
                            table.PrimaryKey = new[] { column };
                        }
                        else
                        {
                            List<DataColumn> primaryKeyCollection = table.PrimaryKey.ToList();
                            primaryKeyCollection.Add(column);
                            table.PrimaryKey = primaryKeyCollection.ToArray();
                        }
                    }
                }
                schema.Tables.Add(table);
            }

            _documentsTable = FindTable(SqlServer.DefaultSchemaName, DocumentsTableName);
            _namesTable = FindTable(SqlServer.DefaultSchemaName, NameMappingTableName);
            _initialized = true;
        }

        public void ImportDocument(int documentId, string providerName, XmlDocument content)
        {
            string schemaName = providerName.ToSqlName();

            if (new List<string> { SqlServer.DefaultSchemaName, "sys", "information_schema" }.Contains(schemaName.ToLower()))
            {
                throw new ArgumentException(String.Format("The provider name '{0}' is not valid (it is a reserved name).", providerName));
            }
            
            EnsureSchema(schemaName);

            Import(schemaName, null, content.DocumentElement, documentId);
        }

        private Schema EnsureSchema(string schemaName)
        {
            Schema schema = _schemas.Find(x => x.Name == schemaName);

            if (schema == null)
            {
                schema = new Schema();
                schema.Name = schemaName;
                _schemas.Add(schema);
            }

            return schema;
        }

        private void Import(string schemaName, DataRow parent, XmlNode content, int? documentId = null)
        {
            string tableNameFromXml = content.Name.ToSqlName();
            int maxTableNameLength = _settings.UseForeignKeys ? _settings.MaximumNameLength - Columns.Identity.Length : _settings.MaximumNameLength;
            string actualTableName = _nameHandler.GetValidName(tableNameFromXml, maxTableNameLength, _settings.NameLengthEnforcementStyle);

            DataTable table = CreateOrFindTable(schemaName, actualTableName, parent);

            List<XmlNode> nestedNodes = new List<XmlNode>();
            Dictionary<string, object> data = new Dictionary<string, object>();

            if (parent == null)
            {
                data.Add(Columns.DocumentId, documentId.Value);
            }

            foreach (XmlAttribute attribute in content.GetAttributes())
            {
                if (!attribute.IsStructuralAttribute())
                {
                    AddValue(table, data, attribute.Name, attribute.Value);
                }
            }

            List<XmlNode> childNodesCollection = content.ChildNodes.Cast<XmlNode>().ToList();
            if (content.IsList())
            {
                nestedNodes.AddRange(childNodesCollection);
            }
            else
            {
                foreach (XmlNode childNode in childNodesCollection)
                {
                    List<XmlNode> nestedChildren = childNode.GetNestedChildren();
                    if (nestedChildren.Any())
                    {
                        nestedNodes.Add(childNode);
                    }
                    else
                    {
                        AddValue(table, data, childNode.Name, childNode.InnerText, childNode.IsNull());
                    }
                }
            }

            DataRow firstRow = table.NewRow();
            AddParentLinks(parent, firstRow);
            foreach (KeyValuePair<string, object> tuple in data)
            {
                firstRow[tuple.Key] = tuple.Value ?? DBNull.Value;
            }
            table.Rows.Add(firstRow);

            if (nestedNodes.Count > 0 || table.ExtendedProperties.GetValue<bool>(HasContentPropertyName))
            {
                List<DataTable> tables = FindSchema(schemaName).Tables;
                if (!tables.Contains(table))
                {
                    tables.Add(table);
                    if (actualTableName != tableNameFromXml)
                    {
                        AddOriginalName(actualTableName, null, tableNameFromXml);
                    }
                }
            }

            foreach (XmlNode nestedNode in nestedNodes)
            {
                Import(schemaName, firstRow, nestedNode);
            }
        }

        private void AddValue(DataTable table, Dictionary<string, object> data, string elementName, string value, bool isNull = false)
        {
            bool hasContent = !String.IsNullOrWhiteSpace(value);
            if (hasContent || isNull)
            {
                string columnName = AddColumnFromXml(table, elementName, value);
                AdjustDataType(table, columnName, value);
                data.Add(columnName, GetTypedValue(table, columnName, value));
            }
        }

        private DataTable CreateOrFindTable(string schemaName, string tableName, DataRow parent)
        {
            DataTable table = FindTable(schemaName, tableName);

            if (table == null)
            {
                table = new DataTable {TableName = tableName};
                table.ExtendedProperties.Add(WriteStatePropertyName, PersistenceState.NotCreated);
                table.ExtendedProperties.Add(HasContentPropertyName, false);
                _foreignKeys.Add(table, new List<DataForeignKey>());

                DataColumn identityColumn = AddNewColumn(table, Columns.Identity, SqlDbType.Int, allowDbNull: false);
                identityColumn.Unique = true;
                identityColumn.AutoIncrement = true;
                identityColumn.AutoIncrementSeed = 1;

                DataColumn primaryKeyColumn;
                if (parent == null)
                {
                    primaryKeyColumn = AddNewColumn(table, Columns.DocumentId, SqlDbType.Int, allowDbNull: false);
                    AddForeignKey(_documentsTable.Columns[Columns.DocumentId], primaryKeyColumn);
                }
                else
                {
                    primaryKeyColumn = identityColumn;
                }
                table.PrimaryKey = new[] {primaryKeyColumn};
            }

            return table;
        }

        private DataTable FindTable(string schemaName, string tableName)
        {
            Schema schema = FindSchema(schemaName);
            return schema.Tables.Find(x => x.TableName.Equals(tableName, StringComparison.CurrentCultureIgnoreCase));
        }

        private Schema FindSchema(string schemaName)
        {
            return _schemas.Find(x => x.Name == schemaName);
        }

        private string AddColumnFromXml(DataTable table, string elementName, string value)
        {
            string proposedColumnName = BuildColumnName(table, elementName);
            string actualColumnName = _nameHandler.GetValidName(proposedColumnName, _settings.MaximumNameLength, _settings.NameLengthEnforcementStyle);

            if (!table.Columns.Contains(actualColumnName))
            {
                int? maxLength = null;
                SqlDbType? columnType = _dataTypeHelper.SuggestType(null, value.ToNullPreferredString());
                if (!columnType.HasValue)
                {
                    columnType = SqlDbType.NVarChar;
                }
                if (columnType == SqlDbType.NVarChar)
                {
                    maxLength = MinimumColumnCharacters;
                }

                AddNewColumn(table, actualColumnName, columnType.Value, maxLength: maxLength);
                table.ExtendedProperties[HasContentPropertyName] = true;

                if (actualColumnName != proposedColumnName)
                {
                    AddOriginalName(table.TableName, actualColumnName, proposedColumnName);
                }
            }

            return actualColumnName;
        }

        private string BuildColumnName(DataTable table, string rawName)
        {
            string name = rawName.ToSqlName().Trim();
            if (name.StartsWith(table.TableName, StringComparison.CurrentCultureIgnoreCase))
            {
                name = name.Replace(table.TableName, String.Empty);
            }
            if (name.StartsWith("-"))
            {
                name = name.Substring(1, name.Length - 1);
            }

            if (_reservedColumns.Exists(x => x.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
            {
                name = string.Format("provider_{0}", name);
            }

            return name;
        }

        private DataColumn AddNewColumn(DataTable table, string columnName, SqlDbType dataType, bool allowDbNull = true, int? maxLength = null)
        {
            Type clrType = (Type) _sqlDbTypeConverter.ConvertTo(dataType, typeof (Type));
            DataColumn column = new DataColumn(columnName, clrType) { AllowDBNull = allowDbNull };

            if (maxLength.HasValue)
            {
                column.MaxLength = maxLength.Value;
            }

            column.ExtendedProperties.Add(WriteStatePropertyName, PersistenceState.NotCreated);

            table.Columns.Add(column);
            return column;
        }

        private void AddOriginalName(string tableName, string columnName, string originalName)
        {
            DataRow row = _namesTable.NewRow();
            row["TableName"] = tableName;
            row["ColumnName"] = columnName ?? String.Empty;
            row["OriginalName"] = originalName;
            _namesTable.Rows.Add(row);
        }

        internal void AdjustDataType(DataTable table, string columnName, string value)
        {
            bool changed = false;
            DataColumn column = table.Columns[columnName];

            SqlDbType? currentType;
            if (column.DataType == typeof (string) && column.MaxLength == MinimumColumnCharacters)
            {
                currentType = null;
            }
            else
            {
                currentType = (SqlDbType) _sqlDbTypeConverter.ConvertFrom(column.DataType);
            }

            SqlDbType? suggestedType = _dataTypeHelper.SuggestType(currentType, value.ToNullPreferredString());

            if (suggestedType.HasValue)
            {
                SqlDbType suggestion = suggestedType.Value;
                if (suggestion != currentType)
                {
                    column = ChangeColumnDataType(table, columnName, suggestion);
                    changed = true;
                }
            }

            if (column.DataType == typeof (string))
            {
                changed = AdjustCharColumnWidth(column, value);
            }

            if (changed && column.ExtendedProperties.GetValue<PersistenceState>(WriteStatePropertyName) == PersistenceState.Written)
            {
                column.ExtendedProperties[WriteStatePropertyName] = PersistenceState.PendingChanges;
            }
        }

        private DataColumn ChangeColumnDataType(DataTable table, string columnName, SqlDbType newDataType)
        {
            DataColumn column = table.Columns[columnName];

            List<Dictionary<string, object>> oldRowData = new List<Dictionary<string, object>>();
            for (int r = 0; r < table.Rows.Count; r++)
            {
                DataRow dataRow = table.Rows[r];
                Dictionary<string, object> tuples = new Dictionary<string, object>();
                foreach (DataColumn dataColumn in table.Columns)
                {
                    tuples.Add(dataColumn.ColumnName, dataRow[dataColumn.ColumnName]);
                }
                oldRowData.Add(tuples);
            }
            table.Rows.Clear();

            Type type = (Type) _sqlDbTypeConverter.ConvertTo(newDataType, typeof (Type));
            table.Columns.Remove(column);
            DataColumn newColumn = RedoColumn(column, type);
            table.Columns.Add(newColumn);
            column = newColumn;

            foreach (Dictionary<string, object> oldColumnValues in oldRowData)
            {
                DataRow redoneDataRow = table.NewRow();

                foreach (KeyValuePair<string, object> tuple in oldColumnValues)
                {
                    object itemValue = tuple.Value;
                    if (tuple.Key == column.ColumnName && itemValue != null && itemValue != DBNull.Value)
                    {
                        itemValue = _dataTypeHelper.ConvertTo(column.DataType, itemValue.ToString());
                    }
                    redoneDataRow[tuple.Key] = itemValue;
                }

                table.Rows.Add(redoneDataRow);
            }

            return column;
        }

        private DataColumn RedoColumn(DataColumn originalColumn, Type newtype)
        {
            DataColumn column = new DataColumn();
            column.ColumnName = originalColumn.ColumnName;
            column.DataType = newtype;
            column.AllowDBNull = originalColumn.AllowDBNull;
            foreach (DictionaryEntry extendedProperty in originalColumn.ExtendedProperties)
            {
                column.ExtendedProperties.Add(extendedProperty.Key, extendedProperty.Value);
            }
            return column;
        }

        private static bool AdjustCharColumnWidth(DataColumn column, string value)
        {
            int currentLength = column.MaxLength;

            int dataLength = string.IsNullOrWhiteSpace(value) ? 0 : value.Length;
            column.MaxLength = Math.Max(dataLength + MinimumColumnCharacters, currentLength);
            if (column.MaxLength > NvarcharBoundedThreshold)
            {
                column.MaxLength = Int32.MaxValue;
            }

            return column.MaxLength > currentLength;
        }

        private void AddParentLinks(DataRow parentRow, DataRow currentRow)
        {
            if (parentRow != null)
            {
                DataTable parentTable = parentRow.Table;
                DataTable currentTable = currentRow.Table;

                if (_settings.UseForeignKeys)
                {
                    string columnName = String.Format("{0}{1}", parentTable.TableName, Columns.Identity);
                    if (!currentTable.Columns.Contains(columnName))
                    {
                        DataColumn column = AddNewColumn(currentTable, columnName, SqlDbType.Int);
                        AddForeignKey(parentTable.Columns[Columns.Identity], column);
                        currentTable.ExtendedProperties[HasContentPropertyName] = true;
                    }
                    currentRow[columnName] = parentRow[Columns.Identity];
                }
                else
                {
                    if (!currentTable.Columns.Contains(Columns.ParentTableName))
                    {
                        AddNewColumn(currentTable, Columns.ParentTableName, SqlDbType.NVarChar, allowDbNull: false, maxLength: MinimumColumnCharacters);
                        AddNewColumn(currentTable, Columns.ParentID, SqlDbType.Int, allowDbNull: false);
                    }

                    currentRow[Columns.ParentTableName] = parentTable.TableName;
                    currentRow[Columns.ParentID] = parentRow[Columns.Identity];
                }
            }
        }

        private void AddForeignKey(DataColumn parentColumn, DataColumn childColumn)
        {
            DataTable table = childColumn.Table;

            string constraintName = String.Format("FK_{0}_{1}", table.TableName, parentColumn.Table.TableName);
            DataForeignKey constraint = new DataForeignKey(constraintName, parentColumn, childColumn);
            
            _foreignKeys[table].Add(constraint);
        }

        private object GetTypedValue(DataTable table, string columnName, string value)
        {
            return _dataTypeHelper.ConvertTo(table.Columns[columnName].DataType, value.ToNullPreferredString());
        }

        public void SaveChanges(SqlTransactionExtended transaction)
        {
            transaction.Finished += OnTransactionFinished;
            WriteDatabaseChanges(transaction);
        }

        private void WriteDatabaseChanges(SqlTransaction transaction)
        {
            StringBuilder foreignKeysScript = new StringBuilder();

            foreach (Schema schema in _schemas)
            {
                string schemaName = schema.Name;

                if (!schema.Created)
                {
                    CreateObjects(transaction, String.Format("CREATE SCHEMA [{0}]", schemaName));
                }

                foreach (DataTable table in schema.Tables)
                {
                    if (table != _namesTable)
                    {
                        SaveTableChanges(transaction, schemaName, table, foreignKeysScript);
                    }
                }

                CreateObjects(transaction, foreignKeysScript.ToString());
            }

            WriteRows(transaction, SqlServer.DefaultSchemaName, _namesTable);
        }

        private void SaveTableChanges(SqlTransaction transaction, string schemaName, DataTable table, StringBuilder foreignKeysScript)
        {
            StringBuilder tableScript = new StringBuilder();
            string qualifiedTableName = GetQualifiedTableName(schemaName, table);

            bool createWholeTable = table.ExtendedProperties.GetValue<PersistenceState>(WriteStatePropertyName) == PersistenceState.NotCreated;
            if (createWholeTable)
            {
                tableScript.AppendFormat("CREATE TABLE {0}", qualifiedTableName).AppendLine()
                           .Append("(").AppendLine();
            }

            foreach (DataColumn column in table.Columns)
            {
                string columnSql = GetColumnSql(column);
                if (createWholeTable)
                {
                    tableScript.AppendFormat("\t{0},", columnSql).AppendLine();
                }
                else
                {
                    PersistenceState persistenceState = column.ExtendedProperties.GetValue<PersistenceState>(WriteStatePropertyName);
                    if (persistenceState == PersistenceState.NotCreated)
                    {
                        tableScript.AppendFormat("ALTER TABLE {0} ADD {1};", qualifiedTableName, columnSql).AppendLine();
                    }
                    else if (persistenceState == PersistenceState.PendingChanges)
                    {
                        tableScript.AppendFormat("ALTER TABLE {0} ALTER COLUMN {1};", qualifiedTableName, columnSql)
                                   .AppendLine();
                    }
                }
            }

            if (createWholeTable)
            {
                tableScript.AppendFormat("\tPRIMARY KEY NONCLUSTERED ( [{0}] ASC )", table.PrimaryKey.First().ColumnName)
                           .AppendLine();
                tableScript.Append(");").AppendLine()
                           .AppendLine();
            }

            foreach (DataForeignKey relation in _foreignKeys[table])
            {
                if (foreignKeysScript.Length > 0)
                {
                    foreignKeysScript.Append(SqlServer.DefaultBatchSeparator).AppendLine();
                }

                if (relation.ObjectState == PersistenceState.NotCreated)
                {
                    string parentTableName =
                        relation.ParentTable.TableName == DocumentsTableName
                            ? GetQualifiedTableName(SqlServer.DefaultSchemaName, _documentsTable)
                            : GetQualifiedTableName(schemaName, relation.ParentTable);

                    string foreignKeySql =
                        BuildForeignKeyConstraintSql(
                            relation.ConstraintName,
                            relation.ChildColumn.ColumnName,
                            parentTableName,
                            relation.ParentColumn.ColumnName);

                    foreignKeysScript.AppendFormat("ALTER TABLE {0} WITH CHECK ADD {1};", qualifiedTableName, foreignKeySql)
                                     .AppendLine();
                }
            }

            CreateObjects(transaction, tableScript.ToString());
            WriteRows(transaction, schemaName, table, SqlBulkCopyOptions.KeepIdentity);
        }

        private static string BuildForeignKeyConstraintSql(string constraintName, string childColumnName, string parentTableName, string parentColumnName)
        {
            return String.Format("CONSTRAINT [{0}] FOREIGN KEY ( [{1}] ) REFERENCES {2} ( [{3}] )", constraintName, childColumnName, parentTableName, parentColumnName);
        }

        private string GetColumnSql(DataColumn column)
        {
            string dataTypeSpecification;
            
            if (column.DataType == typeof(string))
            {
                dataTypeSpecification = String.Format("NVARCHAR({0})", column.MaxLength == Int32.MaxValue ? "MAX" : column.MaxLength.ToString());
            }
            else if (column.DataType == typeof (int))
            {
                dataTypeSpecification = "INT";
                if (column.AutoIncrement)
                {
                    dataTypeSpecification = String.Format("{0} IDENTITY({1},{2})", dataTypeSpecification, column.AutoIncrementSeed, column.AutoIncrementStep);
                }
            }
            else
            {
                SqlDbType dataType = (SqlDbType)_sqlDbTypeConverter.ConvertFrom(column.DataType);
                dataTypeSpecification = dataType.ToString().ToUpper();
            }

            return String.Format("[{0}] {1} {2} {3}", 
                column.ColumnName, 
                dataTypeSpecification, 
                !column.AllowDBNull ? "NOT NULL" : "NULL", 
                column.Unique ? "UNIQUE" : null).Trim();
        }

        private void CreateObjects(SqlTransaction transaction, string objectCreationSql)
        {
            if (objectCreationSql.Length > 0)
            {
                try
                {
                    foreach (string sqlStatement in objectCreationSql.ToSqlStatements())
                    {
                        using (SqlCommand createObjectsCommand = new SqlCommand(sqlStatement))
                        {
                            createObjectsCommand.Connection = transaction.Connection;
                            createObjectsCommand.Transaction = transaction;
                            createObjectsCommand.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception mainException)
                {
                    throw new InvalidOperationException(String.Format("Failed while executing: {0}{1}", Environment.NewLine, objectCreationSql), mainException);
                }
            }
        }

        private static void WriteRows(SqlTransaction transaction, string schemaName, DataTable source, SqlBulkCopyOptions options = SqlBulkCopyOptions.Default)
        {
            using (SqlBulkCopy copier = new SqlBulkCopy(transaction.Connection, options, transaction))
            {
                copier.DestinationTableName = GetQualifiedTableName(schemaName, source);
                copier.BatchSize = WriteBatchSize;
                copier.NotifyAfter = copier.BatchSize;

                foreach (DataColumn column in source.Columns)
                {
                    copier.ColumnMappings.Add(new SqlBulkCopyColumnMapping(column.ColumnName, column.ColumnName));
                }

                copier.WriteToServer(source);
            }
        }

        private static string GetQualifiedTableName(string schemaName, DataTable source)
        {
            return String.Format("[{0}].[{1}]", schemaName, source.TableName);
        }

        private void OnTransactionFinished(object sender, TransactionFinishedEventArgs eventDetails)
        {
            if (eventDetails.Committed)
            {
                UpdateObjectPersistenceStates();
            }
        }

        private void UpdateObjectPersistenceStates()
        {
            foreach (Schema schema in _schemas)
            {
                schema.Created = true;

                foreach (DataTable table in schema.Tables)
                {
                    table.ExtendedProperties[WriteStatePropertyName] = PersistenceState.Written;
                    foreach (DataColumn column in table.Columns)
                    {
                        column.ExtendedProperties[WriteStatePropertyName] = PersistenceState.Written;
                    }
                    foreach (DataForeignKey relation in _foreignKeys[table])
                    {
                        relation.ObjectState = PersistenceState.Written;
                    }
                    table.Rows.Clear();
                }
            }
            _namesTable.Rows.Clear();
        }
    }
}