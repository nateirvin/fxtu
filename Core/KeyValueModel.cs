﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Xml;
using XmlToTable.Core.Properties;
using XmlToTable.Core.Upgrades;

namespace XmlToTable.Core
{
    internal class KeyValueModel : IXmlToTableAdapter
    {
        private const string DefaultDataKind = "text";

        private readonly List<Tuple<string, SqlDbType>> _mapping = 
            new List<Tuple<string, SqlDbType>>
            {
                new Tuple<string, SqlDbType>(DefaultDataKind, SqlDbType.NVarChar),
                new Tuple<string, SqlDbType>("date", SqlDbType.DateTime),
                new Tuple<string, SqlDbType>("unique id", SqlDbType.UniqueIdentifier),
                new Tuple<string, SqlDbType>("number", SqlDbType.Float),
                new Tuple<string, SqlDbType>("true/false", SqlDbType.Bit)
            };

        private readonly IAdapterSettings _settings;
        private Dictionary<string, Variable> _variables;
        private List<DocumentVariable> _documentVariables;
        private readonly IDataTypeHelper _dataTypeHelper;

        public KeyValueModel(IAdapterSettings settings = null)
        {
            _settings = settings ?? new AdapterSettings();
            _dataTypeHelper = new DataTypeHelper();
        }

        public string DatabaseCreationScript
        {
            get { return Resources.VerticalDatabaseCreationScript; }
        }

        public IEnumerable<IUpgrade> Upgrades
        {
            get
            {
                return new List<IUpgrade>
                {
                    new EmbeddedXmlUpgrade(_settings.UpgradeDocumentsQuery),
                    new LongestValueUpgrade(),
                    new NumberToTextUpgrade()
                };
            }
        }

        public void Initialize(SqlConnection repositoryConnection)
        {
            if (_variables == null)
            {
                _variables = new Dictionary<string, Variable>();

                DataSet container = repositoryConnection.GetDataSetFromProcedure(SqlStatements.usp_GetAllVariables);
                DataTable variableData = container.Tables[0];
                foreach (DataRow row in variableData.Rows)
                {
                    string variableName = row[Columns.VariableName].ToString();
                    string dataKind = row[Columns.DataKind].ToString();
                    int? maxLength = row[Columns.LongestValueLength] == DBNull.Value ? (int?) null : (int)row[Columns.LongestValueLength];

                    _variables.Add(variableName,
                        new Variable
                        {
                            XPath = variableName,
                            DataKind = dataKind,
                            LongestValueLength = maxLength,
                            Saved = true
                        });
                }

                _documentVariables = new List<DocumentVariable>();
            }
        }

        public void ImportDocument(int documentId, string providerName, XmlDocument content)
        {
            if (content.DocumentElement != null)
            {
                string rootXPath = string.Format("/{0}", content.DocumentElement.Name);
                AddVariablesFromAttributes(documentId, content.DocumentElement, rootXPath);
                Import(documentId, rootXPath, content.DocumentElement);
            }
        }

        private void Import(int documentId, string parentXPath, XmlNode node)
        {
            List<XmlNode> childNodesCollection = node.GetNestedChildren();
            Dictionary<string, int> elementSequences = new Dictionary<string, int>();

            foreach (XmlNode childNode in childNodesCollection)
            {
                bool isValueElement = !childNode.HasNestedNodes();

                if (!isValueElement || !childNode.IsEmpty())
                {
                    StringBuilder childNodePath = new StringBuilder();
                    childNodePath.AppendFormat("{0}/{1}", parentXPath, childNode.Name);
                    if (node.IsList())
                    {
                        string childNodeName = childNode.Name.ToLower();
                        if (!elementSequences.ContainsKey(childNodeName))
                        {
                            elementSequences.Add(childNodeName, 0);
                        }
                        childNodePath.AppendFormat("[{0}]", ++elementSequences[childNodeName]);
                    }
                    string currentXPath = childNodePath.ToString();

                    AddVariablesFromAttributes(documentId, childNode, currentXPath);

                    if (isValueElement)
                    {
                        AddDocumentVariable(documentId, currentXPath, childNode.InnerText);
                    }
                    else
                    {
                        Import(documentId, currentXPath, childNode);
                    }
                }
            }
        }

        private void AddVariablesFromAttributes(int documentId, XmlNode node, string currentXPath)
        {
            foreach (XmlAttribute attribute in node.GetAttributes())
            {
                if (!attribute.IsStructuralAttribute())
                {
                    AddDocumentVariable(documentId, string.Format("{0}/@{1}", currentXPath, attribute.Name), attribute.Value);
                }
            }
        }

        private void AddDocumentVariable(int documentId, string currentXPath, string rawValue)
        {
            string useableValue = rawValue.ToNullPreferredString();

            if (!_variables.ContainsKey(currentXPath))
            {
                _variables.Add(currentXPath, new Variable { XPath = currentXPath, Saved = false });
            }
            Variable variable = _variables[currentXPath];
            
            SqlDbType? currentType = GetSqlTypeFromKindName(variable.DataKind);
            SqlDbType? suggestedType = _dataTypeHelper.SuggestType(currentType, useableValue);
            if (suggestedType.HasValue)
            {
                variable.DataKind = GetDataKindNameFromSqlType(suggestedType.Value);
                variable.Saved = false;
            }

            if (useableValue != null)
            {
                if (useableValue.Length > variable.LongestValueLength.GetValueOrDefault())
                {
                    variable.LongestValueLength = useableValue.Length;
                    variable.Saved = false;
                }
            }

            _documentVariables.Add(new DocumentVariable
            {
                DocumentID = documentId,
                Variable = variable,
                Value = useableValue
            });
        }

        private string GetDataKindNameFromSqlType(SqlDbType dataType)
        {
            if (dataType.IsOneOf(SqlDbType.BigInt, SqlDbType.Int))
            {
                dataType = SqlDbType.Float;
            }

            return _mapping.Find(x => x.Item2 == dataType).Item1;
        }

        private SqlDbType? GetSqlTypeFromKindName(object dataKindName)
        {
            if (dataKindName == null || dataKindName == DBNull.Value)
            {
                return null;
            }

            return _mapping.Find(x => x.Item1 == dataKindName.ToString()).Item2;
        }

        public void SaveChanges(SqlTransactionExtended transaction)
        {
            transaction.Finished += OnTransactionFinished;
            SaveDocumentVariables(transaction);
            SaveVariableMetadataChanges(transaction);
        }

        private void SaveDocumentVariables(SqlTransactionExtended transaction)
        {
            DataTable parameterValue = new DataTable();
            parameterValue.Columns.Add(Columns.DocumentId, typeof (int));
            parameterValue.Columns.Add(Columns.XPathColumnName, typeof (string));
            parameterValue.Columns.Add(Columns.ValueColumnName, typeof (string));

            foreach (DocumentVariable documentVariable in _documentVariables)
            {
                DataRow row = parameterValue.NewRow();
                row[Columns.DocumentId] = documentVariable.DocumentID;
                row[Columns.XPathColumnName] = documentVariable.Variable.XPath;
                row[Columns.ValueColumnName] = (object) documentVariable.Value ?? DBNull.Value;
                parameterValue.Rows.Add(row);
            }

            ExecuteMergeProcedure(transaction, SqlStatements.usp_InsertVariables, "@DocumentVariables", parameterValue);
        }

        private void SaveVariableMetadataChanges(SqlTransactionExtended transaction)
        {
            DataTable parameterValue = new DataTable();
            parameterValue.Columns.Add(Columns.VariableName, typeof(string));
            parameterValue.Columns.Add(Columns.DataKind, typeof(string));
            parameterValue.Columns.Add(Columns.LongestValueLength, typeof(int));

            foreach (Variable variable in _variables.Values)
            {
                if (!variable.Saved)
                {
                    DataRow parameterRow = parameterValue.NewRow();

                    parameterRow[Columns.VariableName] = variable.XPath;
                    parameterRow[Columns.DataKind] = variable.DataKind ?? DefaultDataKind;
                    parameterRow[Columns.LongestValueLength] =
                        variable.LongestValueLength.HasValue
                            ? (object) variable.LongestValueLength.Value
                            : DBNull.Value;

                    parameterValue.Rows.Add(parameterRow);
                }
            }
            
            ExecuteMergeProcedure(transaction, SqlStatements.usp_UpdateVariables, "@Updates", parameterValue);
        }

        private static void ExecuteMergeProcedure(SqlTransactionExtended transaction, string procedureName, string parameterName, DataTable parameterValue)
        {
            using (SqlCommand insertCommand = new SqlCommand())
            {
                insertCommand.Connection = transaction.Connection as SqlConnection;
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = procedureName;
                insertCommand.CommandType = CommandType.StoredProcedure;
                insertCommand.Parameters.AddWithValue(parameterName, parameterValue);
                insertCommand.CommandTimeout = 60;
                insertCommand.ExecuteNonQuery();
            }
        }

        private void OnTransactionFinished(object sender, TransactionFinishedEventArgs eventDetails)
        {
            if (eventDetails.Committed)
            {
                _documentVariables.Clear();
                foreach (Variable variable in _variables.Values)
                {
                    variable.Saved = true;
                }
            }
        }
    }
}