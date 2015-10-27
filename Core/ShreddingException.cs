using System;

namespace XmlToTable.Core
{
    public class ShreddingException : Exception
    {
        public ShreddingException(Exception innerException)
            : base("Shredding failed.", innerException)
        {
        }
    }
}