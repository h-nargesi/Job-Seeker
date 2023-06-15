using System.Text.RegularExpressions;
using Microsoft.CSharp.RuntimeBinder;

namespace Photon.JobSeeker
{
    struct JobOption
    {
        public string Category { get; set; }

        public long Score { get; set; }

        public string Title { get; set; }

        public Regex Pattern { get; set; }

        public dynamic? Settings { get; set; }

        public override string ToString()
        {
            return $"({Score}) {Title}";
        }

        public string ToString(char state, long? score = null)
        {
            return $"({state}{score ?? Score}) {Title}";
        }

        public void AddKeyword(HashSet<string> keywords, string matched)
        {
            var keyword = ResumeKeyword(out var parrent, out var include_sub);
            if (keyword != null)
            {
                if (parrent != null) parrent += ":";
                keywords.Add(parrent + keyword);

                if (include_sub)
                {
                    if (parrent == null) parrent = keyword;
                    var sub = matched.Split('\n')
                                     .Where(x => x.Length > 0 && keyword != x.MainOption())
                                     .Select(x => parrent + ":" + x.SubOption()).ToArray();
                    keywords.UnionWith(sub);
                }
            }
        }

        private string? ResumeKeyword(out string? parrent, out bool include_sub)
        {
            string? keyword = null;
            include_sub = true;
            parrent = null;

            if (Settings == null) keyword = Title;
            else try
                {
                    dynamic resume = Settings.resume;

                    if (resume == null) return null;

                    try
                    {
                        keyword = (string)resume.key;
                        if (keyword == null) keyword = Title;
                    }
                    catch (RuntimeBinderException) { keyword = Title; }

                    try { include_sub = (bool)resume.sub; }
                    catch (RuntimeBinderException) { }

                    try
                    {
                        parrent = (string)resume.parent;
                        parrent = parrent?.MainOption();
                    }
                    catch (RuntimeBinderException) { }
                }
                catch (RuntimeBinderException) { keyword = Title; }

            if (parrent == null) keyword = keyword.MainOption();

            return keyword;
        }

    }

    public static class JobOptionExtension
    {
        public static string MainOption(this string text)
        {
            return main_replace.Replace(text.ToUpper(), "_");
        }

        public static string SubOption(this string text)
        {
            var words = text.Split(' ')
                            .Select(x => x[0..1].ToUpper() + x[1..]);

            return string.Join(" ", words);
        }

        private static readonly Regex main_replace = new("-| ");
    }
}