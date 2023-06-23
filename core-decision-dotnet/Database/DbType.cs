using System.Collections;
using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    static class DbType
    {
        public static (System.Data.DbType type, object value) GetSqliteType(object value)
        {
            if (value is DBNull)
                throw new ArgumentNullException(nameof(value));

            if (value is Type type)
                value = DBNull.Value;
            else type = value.GetType();

            if (type.IsEnum)
            {
                type = typeof(string);
                if (value is not DBNull)
                    value = value.ToString() ?? (object)DBNull.Value;
            }
            else if (!SYSTEM_TYPE_MAP.ContainsKey(type))
            {
                type = typeof(string);
                if (value is not DBNull)
                    value = JsonConvert.SerializeObject(value);
            }

            return (SYSTEM_TYPE_MAP[type], value);
        }

        private readonly static Dictionary<Type, System.Data.DbType> SYSTEM_TYPE_MAP = new()
        {
            [typeof(byte)] = System.Data.DbType.Int64,
            [typeof(sbyte)] = System.Data.DbType.Int64,
            [typeof(short)] = System.Data.DbType.Int64,
            [typeof(ushort)] = System.Data.DbType.Int64,
            [typeof(int)] = System.Data.DbType.Int64,
            [typeof(uint)] = System.Data.DbType.Int64,
            [typeof(long)] = System.Data.DbType.Int64,
            [typeof(ulong)] = System.Data.DbType.Int64,
            [typeof(float)] = System.Data.DbType.Double,
            [typeof(double)] = System.Data.DbType.Double,
            [typeof(decimal)] = System.Data.DbType.Double,
            [typeof(bool)] = System.Data.DbType.Boolean,
            [typeof(string)] = System.Data.DbType.String,
            [typeof(char)] = System.Data.DbType.String,
            [typeof(Guid)] = System.Data.DbType.Guid,
            [typeof(DateTime)] = System.Data.DbType.DateTime,
            [typeof(DateTimeOffset)] = System.Data.DbType.DateTime,
            [typeof(byte[])] = System.Data.DbType.Binary,
            [typeof(byte?)] = System.Data.DbType.Int64,
            [typeof(sbyte?)] = System.Data.DbType.Int64,
            [typeof(short?)] = System.Data.DbType.Int64,
            [typeof(ushort?)] = System.Data.DbType.Int64,
            [typeof(int?)] = System.Data.DbType.Int64,
            [typeof(uint?)] = System.Data.DbType.Int64,
            [typeof(long?)] = System.Data.DbType.Int64,
            [typeof(ulong?)] = System.Data.DbType.Int64,
            [typeof(float?)] = System.Data.DbType.Double,
            [typeof(double?)] = System.Data.DbType.Double,
            [typeof(decimal?)] = System.Data.DbType.Double,
            [typeof(bool?)] = System.Data.DbType.Boolean,
            [typeof(char?)] = System.Data.DbType.String,
            [typeof(Guid?)] = System.Data.DbType.Guid,
            [typeof(DateTime?)] = System.Data.DbType.DateTime,
            [typeof(DateTimeOffset?)] = System.Data.DbType.DateTime,
        };
    }
}