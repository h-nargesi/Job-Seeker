using System.Collections;

namespace Photon.JobSeeker
{
    static class Extensions
    {
        public static string? StringJoin(this object value, string spliter = ", ")
        {
            if (value is string str) return str;

            else if (value is IDictionary<string, object> dict)
                return dict.DictStringJoin(spliter);

            else if (value is IEnumerable list)
                return list.Cast<object>().ListStringJoin(spliter);

            else return value.ToString();
        }

        public static string ListStringJoin<T>(this IEnumerable<T> values, string spliter = ", ")
        {
            return $"[{string.Join(spliter, values.Where(v => v != null).Select(v => v?.StringJoin(spliter)))}]";
        }

        public static string DictStringJoin<K, V>(this IDictionary<K, V> values, string spliter = ", ")
        {
            var list = values.Where(p => p.Key != null)
                             .Select(p => @$"{p.Key}: {p.Value}");

            return $"{{{string.Join(spliter, list)}}}";
        }

        public static Command[] Shift(this Command[] commands, Command @new)
        {
            var list = new List<Command>(commands);
            list.Insert(1, @new);
            return list.ToArray();
        }

        public static TrendType GetTrendType(this TrendState state)
        {
            if (state >= TrendState.Analyzing) return TrendType.Job;
            else if (state >= TrendState.Seeking) return TrendType.Search;
            else if (state >= TrendState.Auth) return TrendType.Login;
            else return TrendType.Blocked;
        }

        public static TrendState GetTrendState(this TrendType type)
        {
            return type switch
            {
                TrendType.Job => TrendState.Analyzing,
                TrendType.Search => TrendState.Seeking,
                TrendType.Login => TrendState.Auth,
                _ => TrendState.Blocked
            };
        }
    }
}