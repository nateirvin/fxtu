using System;

namespace XmlToTable.Core
{
    public class InvalidNameException : Exception
    {
        public InvalidNameException(string message, string value)
            : base(string.Format("Invalid name '{0}'. {1}", value, message))
        {
            Value = value;
        }

        public string Value { get; private set; }
    }
}