using System.Text.RegularExpressions;

namespace Photon.JobSeeker
{
    struct JobOption
    {
        public string Category { get; set; }

        public long Score { get; set; }

        public string Title { get; set; }

        public Regex Pattern { get; set; }

        public JobOptionSettings? Settings { get; set; }

        public override readonly string ToString()
        {
            return $"({Score}) {Title}";
        }

        public readonly string ToString(char state, long? score = null)
        {
            return $"({state}{score ?? Score}) {Title}";
        }

        public readonly void AddKeyword(ResumeContext resume, string matched, out string main)
        {
            if (ResumeKeyword(out main, out var keyword, out var include_matched))
            {
                HashSet<string> list;

                if (!resume.Keys.ContainsKey(main))
                    resume.Keys.Add(main, list = new(StringComparer.OrdinalIgnoreCase));
                else
                {
                    list = resume.Keys[main] ?? new(StringComparer.OrdinalIgnoreCase);
                    resume.Keys[main] = list;
                }

                if (keyword != null) list.Add(keyword);

                if (include_matched)
                {
                    var main_string = main;

                    var m = matched.Split('\n')
                                   .Where(x => x.Length > 0)
                                   .Where(x => !ResumeContext.KeysContext.NotInclude.Contains(x))
                                   .Where(x => main_string != x.MainOption())
                                   .Select(x => x.SubOption()).ToArray();

                    list.UnionWith(m);
                }
            }
        }

        private readonly bool ResumeKeyword(out string main, out string? keyword, out bool include_matched)
        {
            main = string.Empty;
            keyword = null;
            include_matched = true;

            if (Settings == null) main = Title;
            else if (Settings.Resume == null) return false;
            else
            {
                main = Settings.Resume.Key ?? Title;
                include_matched = Settings.Resume.IncludeMatched ?? true;

                if (Settings.Resume.Parent is { } parent)
                {
                    keyword = main;
                    main = parent;
                }
            }

            var MAIN = main.MainOption();

            if (!ResumeContext.KeysContext.MainKeys.Contains(MAIN) && keyword == null)
            {
                keyword = main;
                main = "MORE";
            }
            else main = MAIN;

            return true;
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