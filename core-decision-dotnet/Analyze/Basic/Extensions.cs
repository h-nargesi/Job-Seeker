namespace Photon.JobSeeker
{
    static class Extensions
    {
        public static string StringJoin<T>(this IEnumerable<T> values, string spliter = ", ")
        {
            return string.Join(spliter, values.Where(v => v != null).Select(v => v?.ToString()));
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
            else return TrendType.Search;
        }

        public static TrendState GetTrendState(this TrendType type)
        {
            switch (type)
            {
                case TrendType.Job: return TrendState.Analyzing;
                case TrendType.Search:
                default: return TrendState.Auth;
            }
        }
    }
}