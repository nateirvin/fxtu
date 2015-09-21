using System;

namespace XmlToTable.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigurablePropertyAttribute : Attribute
    {
        public ConfigurablePropertyAttribute(string configKeyName)
        {
            ConfigKeyName = configKeyName;
        }

        public string ConfigKeyName { get; private set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public string ShortcutName { get; set; }
    }
}