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
    }
}