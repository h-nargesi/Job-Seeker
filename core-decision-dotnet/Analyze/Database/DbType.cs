using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    static class DbType
    {
        public static SqliteType GetSqliteType(this Type type)
        {
            if (!SYSTEM_TYPE_MAP.ContainsKey(type))
                throw new ArgumentOutOfRangeException(nameof(type), type.FullName);
            else return SYSTEM_TYPE_MAP[type];
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