using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Globalization;

namespace System.Data
{
    public class SqlDbTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof (Type) || sourceType == typeof(SqlDbType) || sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (value is Type)
            {
                Pair mapping = _mapping.Find(x => x.Type.Equals(value));

                if (mapping == null)
                {
                    throw new SqlTypeException(string.Format("The type '{0}' has no SQL equivalent.", value));
                }

                return mapping.DbType;
            }

            if (value is string)
            {
                string dataTypeName = value.ToString();
                if (dataTypeName.Equals("sysname", StringComparison.InvariantCultureIgnoreCase))
                {
                    return SqlDbType.NVarChar;
                }

                SqlDbType dataType = (SqlDbType) Enum.Parse(typeof(SqlDbType), dataTypeName, true);
                return dataType;
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof (Type) || destinationType == typeof (SqlDbType) || destinationType == typeof(string))
            {
                return true;
            }

            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof (string))
            {
                return value == null ? null : value.ToString();
            }

            if (destinationType == typeof (SqlDbType))
            {
                return value;
            }

            if (destinationType == typeof (Type))
            {
                Pair mapping = _mapping.Find(x => x.DbType.Equals(value));
                return mapping.Type;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        private readonly List<Pair> _mapping = new List<Pair>
        {
            new Pair(typeof(string), SqlDbType.NVarChar),
            new Pair(typeof(bool), SqlDbType.Bit),
            new Pair(typeof(float), SqlDbType.Float),
            new Pair(typeof(decimal), SqlDbType.Decimal),
            new Pair(typeof(DateTime), SqlDbType.DateTime),
            new Pair(typeof(int), SqlDbType.Int),
            new Pair(typeof(long), SqlDbType.BigInt),
            new Pair(typeof(Guid), SqlDbType.UniqueIdentifier),
        };

        private class Pair
        {
            public Pair(Type type, SqlDbType dbType)
            {
                Type = type;
                DbType = dbType;
            }

            public Type Type { get; private set; }
            public SqlDbType DbType { get; private set; }
        }
    }
}