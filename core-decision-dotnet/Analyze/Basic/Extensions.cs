namespace Photon.JobSeeker
{
    static class Extensions
    {
        public static string StringJoin<T>(this IEnumerable<T> values, string spliter = ", ")
        {
            return string.Join(spliter, values.Where(v => v != null).Select(v => v?.ToString()));
        }
    }
}