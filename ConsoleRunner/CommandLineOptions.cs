using System.Collections;
using XmlToTable.Core;

namespace XmlToTable.Console
{
    public class CommandLineOptions : ShreddingEngineSettings
    {
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

        [ConfigurableProperty(SourceConnectionKeyName, IsRequired = false, ShortcutName = SourceConnectionShortcutName)]
        public override string SourceConnectionAddress { get; set; }

        [ConfigurableProperty(SourceSpecificationKeyName, IsRequired = false, ShortcutName = SourceSpecificationShortcutName)]
        public override string SourceSpecification { get; set; }

        [ConfigurableProperty("verbose", DefaultValue = false, ShortcutName = "v")]
        public bool Verbose { get; set; }

        [ConfigurableProperty("creationScriptFilename", ShortcutName = "g")]
        public string CreationScriptFilename { get; set; }

        [ConfigurableProperty("batchSize", DefaultValue = 1000, ShortcutName = "b")]
        public int BatchSize { get; set; }

        [ConfigurableProperty("repeat", DefaultValue = false, ShortcutName = "a")]
        public bool Repeat { get; set; }

        public bool GenerateCreationScript
        {
            get { return !string.IsNullOrWhiteSpace(CreationScriptFilename); }
        }

        protected override void ValidateSetup()
        {
            base.ValidateSetup();

            if (!GenerateCreationScript)
            {
                if (string.IsNullOrWhiteSpace(SourceConnectionAddress))
                {
                    ThrowRequirementException(SourceConnectionKeyName, SourceConnectionShortcutName);
                }
                if (string.IsNullOrWhiteSpace(SourceSpecification))
                {
                    ThrowRequirementException(SourceSpecificationKeyName, SourceSpecificationShortcutName);
                }
            }
        }
    }
}