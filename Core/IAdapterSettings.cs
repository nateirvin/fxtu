namespace XmlToTable.Core
{
    public interface IAdapterSettings
    {
        string RepositoryName { get; }
        bool IsHierarchicalModel { get; }
        int MaximumNameLength { get; }
        TooLongNameBehavior NameLengthEnforcementStyle { get; }
        bool UseForeignKeys { get; }
        string UpgradeDocumentsQuery { get; }
    }
}