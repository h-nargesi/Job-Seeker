﻿using System.Text.RegularExpressions;
using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageSearch : SearchPage, IamExpatPageInterface
    {
        public override int Order => 20;

        public override TrendState TrendState => TrendState.Seeking;

        public IamExpatPageSearch(IamExpat parent) : base(parent) { }

        protected override bool CheckInvalidUrl(string text, out Command[]? commands)
        {
            commands = null;
            return !IamExpatPageInterface.reg_search_url.IsMatch(text);
        }

        protected override bool CheckInvalidSearchTitle(string text, out Command[]? commands)
        {
            if (IamExpatPageInterface.reg_search_title.IsMatch(text))
            {
                commands = null;
                return false;
            }
            else
            {
                commands = FillSearchCommands();

                return true;
            }
        }

        protected override IEnumerable<(string url, string code)> GetJobUrls(string text)
        {
            var result = new List<(string url, string code)>();
            var base_link = Parent.Link.Trim().EndsWith("/") ? Parent.Link[..^1] : Parent.Link;
            var job_matches = IamExpatPageInterface.reg_job_url.Matches(text).Cast<Match>();

            foreach (Match job_match in job_matches)
            {
                var code = IamExpatPageInterface.GetJobCode(job_match);
                var url = string.Join("", base_link, job_match.Value);
                result.Add((url, code));
            }

            return result;
        }

        protected override bool CheckNextButton(string text, out Command[]? commands)
        {
            if (IamExpatPageInterface.reg_search_end.IsMatch(text))
            {
                commands = FillSearchCommands();

                return true;
            }
            else
            {
                commands = null;

                return false;
            }
        }

        private static Command[] FillSearchCommands() => new Command[]
        {
            Command.Click(@"label[for=""industry-260""]"), // it-technology
            Command.Click(@"label[for=""ccareer-level-19926""]"), // entry-level
            Command.Click(@"label[for=""career-level-19928""]"), // experienced
            Command.Click(@"label[for=""contract-19934""]"),
            Command.Wait(3000),
            Command.Click(@"input[type=""submit""][value=""Search""]"),
        };

        private static Command[] NextPageCommand() => new Command[]
        {
            Command.Click(@"a[title=""Go to next page""]")
        };
    }
}