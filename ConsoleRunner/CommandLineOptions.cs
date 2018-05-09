using System.Collections;
using System.Configuration;
using XmlToTable.Core;

namespace XmlToTable.Console
{
    public class CommandLineOptions : ShreddingEngineSettings
    {
        private const string UpgradeScriptKeyName = "upgradeScriptFilename";
        private const string UpgradeScriptShortcutName = "m";

        public CommandLineOptions()
        {
        }

        public CommandLineOptions(Hashtable settings) 
            : base(settings)
        {
        }

        public static bool HasConfig
        {
            get { return GetConfigSection() != null; }
        }

        [ConfigurableProperty(SourceLocationKeyName, IsRequired = false, ShortcutName = SourceLocationShortcutName)]
        public override string SourceLocation { get; set; }

        [ConfigurableProperty(SourceSpecificationKeyName, IsRequired = false, ShortcutName = SourceSpecificationShortcutName)]
        public override string SourceSpecification { get; set; }

        [ConfigurableProperty("verbose", DefaultValue = false, ShortcutName = "v")]
        public bool Verbose { get; set; }

        [ConfigurableProperty("creationScriptFilename", ShortcutName = "g")]
        public string CreationScriptFilename { get; set; }

        [ConfigurableProperty(UpgradeScriptKeyName, ShortcutName = UpgradeScriptShortcutName)]
        public string UpgradeScriptFilename { get; set; }

        [ConfigurableProperty("batchSize", DefaultValue = 1000, ShortcutName = "b")]
        public int BatchSize { get; set; }

        [ConfigurableProperty("repeat", DefaultValue = false, ShortcutName = "a")]
        public bool Repeat { get; set; }

        public bool GenerateCreationScript
        {
            get { return !string.IsNullOrWhiteSpace(CreationScriptFilename); }
        }

        public bool GenerateUpgradeScript
        {
            get { return !string.IsNullOrWhiteSpace(UpgradeScriptFilename); }
        }

        protected override void ValidateSetup()
        {
            base.ValidateSetup();

            if (GenerateCreationScript && GenerateUpgradeScript)
            {
                throw new ConfigurationErrorsException("The creation script contains all changes included in the upgrade script, you do not need both.");
            }

            if (!GenerateCreationScript && !GenerateUpgradeScript)
            {
                if (string.IsNullOrWhiteSpace(SourceLocation))
                {
                    ThrowRequirementException(SourceLocationKeyName, SourceLocationShortcutName);
                }
                if (!IsFileSource && string.IsNullOrWhiteSpace(SourceSpecification))
                {
                    ThrowRequirementException(SourceSpecificationKeyName, SourceSpecificationShortcutName);
                }
            }
        }
    }
}