using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    static class DbType
    {
        public static (SqliteType type, object value) GetSqliteType(object value)
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

            if (!SYSTEM_TYPE_MAP.ContainsKey(type))
                throw new ArgumentOutOfRangeException(nameof(type), type.FullName);

            return (SYSTEM_TYPE_MAP[type], value);
        }

        private readonly static Dictionary<Type, SqliteType> SYSTEM_TYPE_MAP = new Dictionary<Type, SqliteType>
        {
            [typeof(byte)] = SqliteType.Integer,
            [typeof(sbyte)] = SqliteType.Integer,
            [typeof(short)] = SqliteType.Integer,
            [typeof(ushort)] = SqliteType.Integer,
            [typeof(int)] = SqliteType.Integer,
            [typeof(uint)] = SqliteType.Integer,
            [typeof(long)] = SqliteType.Integer,
            [typeof(ulong)] = SqliteType.Integer,
            [typeof(float)] = SqliteType.Real,
            [typeof(double)] = SqliteType.Real,
            [typeof(decimal)] = SqliteType.Real,
            [typeof(bool)] = SqliteType.Integer,
            [typeof(string)] = SqliteType.Text,
            [typeof(char)] = SqliteType.Text,
            [typeof(Guid)] = SqliteType.Text,
            [typeof(DateTime)] = SqliteType.Text,
            [typeof(DateTimeOffset)] = SqliteType.Text,
            [typeof(byte[])] = SqliteType.Blob,
            [typeof(byte?)] = SqliteType.Blob,
            [typeof(sbyte?)] = SqliteType.Integer,
            [typeof(short?)] = SqliteType.Integer,
            [typeof(ushort?)] = SqliteType.Integer,
            [typeof(int?)] = SqliteType.Integer,
            [typeof(uint?)] = SqliteType.Integer,
            [typeof(long?)] = SqliteType.Integer,
            [typeof(ulong?)] = SqliteType.Integer,
            [typeof(float?)] = SqliteType.Real,
            [typeof(double?)] = SqliteType.Real,
            [typeof(decimal?)] = SqliteType.Real,
            [typeof(bool?)] = SqliteType.Integer,
            [typeof(char?)] = SqliteType.Text,
            [typeof(Guid?)] = SqliteType.Text,
            [typeof(DateTime?)] = SqliteType.Text,
            [typeof(DateTimeOffset?)] = SqliteType.Text,
        };
    }
}