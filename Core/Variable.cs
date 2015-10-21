namespace XmlToTable.Core
{
    internal class Variable
    {
        public string XPath { get; set; }
        public string DataKind { get; set; }
        public bool Saved { get; set; }
        public int? LongestValueLength { get; set; }
    }
}