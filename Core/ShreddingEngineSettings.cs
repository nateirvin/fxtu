using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Reflection;

namespace XmlToTable.Core
{
    public class ShreddingEngineSettings : IAdapterSettings
    {
        protected const string ConfigSectionName = "xmlToTable";
        protected const string SourceConnectionKeyName = "sourceConnection";
        protected const string SourceConnectionShortcutName = "sc";
        protected const string SourceSpecificationKeyName = "source";
        protected const string SourceSpecificationShortcutName = "ss";
        public const string UpgradeDocumentsQueryKeyName = "redoDocumentsQuery";

        private string _repositoryHostName;
        private string _repositoryName;

        public ShreddingEngineSettings()
        {
            Hashtable configSettings = GetConfigSection();

            if (configSettings == null)
            {
                throw new ConfigurationErrorsException(string.Format("The section {0} could not be found.", ConfigSectionName));
            }

            PopulateSettings(configSettings);
        }

        public ShreddingEngineSettings(Hashtable settings)
        {
            PopulateSettings(settings);
        }

        protected static Hashtable GetConfigSection()
        {
            return (Hashtable) ConfigurationManager.GetSection(ConfigSectionName);
        }

        [ConfigurableProperty(SourceConnectionKeyName, IsRequired = true, ShortcutName = SourceConnectionShortcutName)]
        public virtual string SourceConnectionAddress { get; set; }

        [ConfigurableProperty(SourceSpecificationKeyName, IsRequired = true, ShortcutName = SourceSpecificationShortcutName)]
        public virtual string SourceSpecification { get; set; }

        [ConfigurableProperty("sourceTimeout", DefaultValue = 30, ShortcutName = "t")]
        public virtual int SourceQueryTimeout { get; set; }

        [ConfigurableProperty("repositoryHost", DefaultValue = "localhost", ShortcutName = "rh")]
        public virtual string RepositoryHostName
        {
            get
            {
                return _repositoryHostName;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException();
                }
                _repositoryHostName = value;
            }
        }

        [ConfigurableProperty("repositoryUsername", ShortcutName = "ru")]
        public virtual string RepositoryUsername { get; set; }

        [ConfigurableProperty("repositoryPassword", ShortcutName = "rp")]
        public virtual string RepositoryPassword { get; set; }

        [ConfigurableProperty("repositoryName", DefaultValue = "XmlData", ShortcutName = "rn")]
        public virtual string RepositoryName
        {
            get
            {
                return _repositoryName;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException();
                }
                _repositoryName = value;
            }
        }

        [ConfigurableProperty("isHierarchicalModel", DefaultValue = true, ShortcutName = "z")]
        public virtual bool IsHierarchicalModel { get; set; }

        [ConfigurableProperty("useForeignKeys", DefaultValue = true, ShortcutName = "f")]
        public virtual bool UseForeignKeys { get; set; }

        [ConfigurableProperty("maximumNameLength", DefaultValue = int.MaxValue, ShortcutName = "l")]
        public virtual int MaximumNameLength { get; set; }

        [ConfigurableProperty(UpgradeDocumentsQueryKeyName, ShortcutName = "i")]
        public string UpgradeDocumentsQuery { get; set; }

        public TooLongNameBehavior NameLengthEnforcementStyle
        {
            get { return DefaultTooLongNameBehavior; }
        }

        [ConfigurableProperty("provider", ShortcutName = "p")]
        public virtual string ProviderToProcess { get; set; }

        private static TooLongNameBehavior DefaultTooLongNameBehavior
        {
            get { return TooLongNameBehavior.Abbreviate; }
        }

        protected virtual void PopulateSettings(Hashtable values)
        {
            bool hasUsedShortcut = false;
            Dictionary<string, string> settings = values.ToDictionary(makeKeysLowercase: true);

            PropertyInfo[] propertyInfos = GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                object[] attributes = propertyInfo.GetCustomAttributes(typeof (ConfigurablePropertyAttribute), false);
                if (attributes.Length == 1)
                {
                    ConfigurablePropertyAttribute attribute = attributes[0] as ConfigurablePropertyAttribute;
                    string configKeyName = attribute.ConfigKeyName.ToLower();
                    string shortcutName = attribute.ShortcutName.ToLower();

                    if (settings.ContainsKey(configKeyName))
                    {
                        SetPropertyFromRawValue(propertyInfo, settings[configKeyName]);
                    }
                    else if (settings.ContainsKey(shortcutName))
                    {
                        hasUsedShortcut = true;
                        SetPropertyFromRawValue(propertyInfo, settings[shortcutName]);
                    }
                    else if (!attribute.IsRequired)
                    {
                        propertyInfo.SetValue(this, attribute.DefaultValue, null);
                    }
                    else
                    {
                        ThrowRequirementException(configKeyName, shortcutName, hasUsedShortcut);
                    }
                }
            }

            ValidateSetup();
        }

        private void SetPropertyFromRawValue(PropertyInfo propertyInfo, string rawValue)
        {
            if (rawValue == null)
            {
                throw new ArgumentNullException("rawValue");
            }

            TypeConverter typeConverter = TypeDescriptor.GetConverter(propertyInfo.PropertyType);
            object typedValue = typeConverter.ConvertFromString(rawValue);

            propertyInfo.SetValue(this, typedValue, null);
        }

        protected static void ThrowRequirementException(string configKeyName, string shortcutName, bool displayShortcut = true)
        {
            string extraInfo = displayShortcut ? string.Format(" [-{0}]", shortcutName) : null;
            throw new ConfigurationErrorsException(string.Format("{0}{1} is required.", configKeyName, extraInfo));
        }

        protected virtual void ValidateSetup()
        {
            if (!string.IsNullOrWhiteSpace(RepositoryUsername) &&
                string.IsNullOrWhiteSpace(RepositoryPassword))
            {
                throw new ConfigurationErrorsException("Password is required when username is supplied.");
            }
        }

        public virtual string GetRepositoryConnectionAddress()
        {
            // ReSharper disable once CollectionNeverUpdated.Local
            SqlConnectionStringBuilder connectionAddressBuilder = new SqlConnectionStringBuilder();

            connectionAddressBuilder.DataSource = RepositoryHostName;
            if (string.IsNullOrWhiteSpace(RepositoryUsername))
            {
                connectionAddressBuilder.IntegratedSecurity = true;
            }
            else
            {
                connectionAddressBuilder.UserID = RepositoryUsername;
                connectionAddressBuilder.Password = RepositoryPassword;
            }
            connectionAddressBuilder.InitialCatalog = "master";
            connectionAddressBuilder.ApplicationName = "XmlToTable";

            return connectionAddressBuilder.ToString();
        }
    }
}