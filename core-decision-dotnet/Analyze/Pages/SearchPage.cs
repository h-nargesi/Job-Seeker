using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Pages
{
    abstract class SearchPage : PageBase
    {
        public override int Order => 20;

        public override TrendState TrendState => TrendState.Seeking;

        protected SearchPage(Agency parent, IPageHandler handler) : base(parent, handler) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            Command[]? commands;

            if (Handler.CheckUrl(url, out commands)) return commands;

            if (CheckSearchTitle(content, out commands)) return commands;

            var codes = new HashSet<string>();
            using var database = Database.Open();

            foreach (var (link, code) in GetJobUrls(content))
            {
                if (string.IsNullOrEmpty(code) || codes.Contains(code)) continue;
                codes.Add(code);

                database.Job.Save(new
                {
                    AgencyID = Parent.ID,
                    Url = link,
                    Code = code,
                    State = JobState.Saved
                });
            }

            if (GetNextButton(content, out commands)) return commands;
            else return new Command[0];
        }

        protected abstract IEnumerable<(string url, string code)> GetJobUrls(string text);

        protected abstract bool CheckSearchTitle(string text, out Command[] commands);

        protected abstract bool GetNextButton(string text, out Command[] commands);
    }
}