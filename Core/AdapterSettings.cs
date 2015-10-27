namespace XmlToTable.Core
{
    internal class AdapterSettings : IAdapterSettings
    {
        public AdapterSettings()
        {
            IsHierarchicalModel = true;
            UseForeignKeys = true;
            MaximumNameLength = int.MaxValue;
            NameLengthEnforcementStyle = TooLongNameBehavior.Throw;
            EmbeddedXmlUpgradeDocumentsQuery = null;
        }

        public string RepositoryName { get; private set; }
        public bool IsHierarchicalModel { get; set; }
        public int MaximumNameLength { get; set; }
        public TooLongNameBehavior NameLengthEnforcementStyle { get; set; }
        public bool UseForeignKeys { get; set; }
        public string EmbeddedXmlUpgradeDocumentsQuery { get; set; }
    }
}