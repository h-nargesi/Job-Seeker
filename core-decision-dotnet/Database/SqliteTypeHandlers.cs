using System.Data;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Photon.JobSeeker;

internal static class SqliteTypeHandlers
{
    private static readonly object register_lock = new();
    private static bool registered;

    public static void Register()
    {
        lock (register_lock)
        {
            if (registered) return;

            Add(new EnumNameTypeHandler<JobState>());
            Add(new EnumNameTypeHandler<TrendState>());
            Add(new EnumNameTypeHandler<TrendType>());
            Add(new EnumNameTypeHandler<AiVerdict>());
            Add(new EnumNameTypeHandler<AiSeniority>());
            Add(new EnumNameTypeHandler<AiWorkModel>());
            Add(new EnumNameTypeHandler<AiContract>());
            Add(new EnumNameTypeHandler<AiPeriod>());
            Add(new ResumeContextTypeHandler());
            Add(new JsonTypeHandler<List<string>>());
            Add(new JsonTypeHandler<ResumeText>());

            registered = true;
        }
    }

    private static void Add<T>(SqlMapper.TypeHandler<T> handler)
    {
        SqlMapper.AddTypeHandler(handler);
        SqlMapper.AddTypeHandler(typeof(T), handler);
    }
}

internal sealed class EnumNameTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override T Parse(object value)
    {
        return value is string name ? (T)Enum.Parse(typeof(T), name) : default;
    }

    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }
}

internal sealed class ResumeContextTypeHandler : SqlMapper.TypeHandler<ResumeContext>
{
    public override ResumeContext Parse(object value)
    {
        if (value is DBNull or null) return null!;

        return JsonConvert.DeserializeObject<ResumeContext>((string)value)!;
    }

    public override void SetValue(IDbDataParameter parameter, ResumeContext? value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value == null ? DBNull.Value : JsonConvert.SerializeObject(value);
    }
}

internal sealed class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter(new CamelCaseNamingStrategy(), false) },
    };

    public override T Parse(object value)
    {
        if (value is DBNull or null) return default!;

        return JsonConvert.DeserializeObject<T>((string)value, Settings)!;
    }

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value == null ? DBNull.Value : JsonConvert.SerializeObject(value, Settings);
    }
}
