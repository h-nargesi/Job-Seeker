using System.Text.RegularExpressions;

class IamExpatPageSearch : IamExpatPage
{
    public override int Order => 10;

    private static readonly Regex reg_search_url = new(@"://[^/]*iamexpat\.nl/career/jobs-netherlands");
    private static readonly Regex reg_search_title = new(@"<h1>[^<]*IT[^<]*Technology[^<]*</h1>");
    private static readonly Regex reg_job_link = new(@"href=[""\'](/career/jobs-[^""\']+/it-technology/[^""\']+/(\d+)/?)[""\']");

    public IamExpatPageSearch(Agency parent) : base(parent)
    {
    }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (!reg_search_url.IsMatch(content)) return null;

        if (!reg_search_title.IsMatch(content))
        {
            return new Command[]
            {
                Command.Click(@"label[for=""industry-260""]"), // it-technology
                Command.Click(@"label[for=""ccareer-level-19926""]"), // entry-level
                Command.Click(@"label[for=""career-level-19928""]"), // experienced
                Command.Click(@"label[for=""contract-19934""]"),
                Command.Wait(3000),
                Command.Click(@"input[type=""submit""][value=""Search""]"),
            };
        }

        var codes = new HashSet<long>();
        using var database = Database.Open();
        
        foreach(Match job in reg_job_link.Matches(content).Cast<Match>())
        {
            var code = long.Parse(job.Groups[1].Value);
            
            if (codes.Contains(code)) continue;
            codes.Add(code);

            database.Execute(q_ins_job, Parent.ID, url, code, JobState.saved.ToString());
        }

        return new Command[] { Command.Click(@"a[title=""Go to next page""]") };
    }


    private const string q_ins_job = @"
INSERT INTO Job (AgencyID, Url, Code, State) VALUES (@agency, @url, @code, @state)
ON CONFLICT(AgencyID, Code) DO NOTHING;";

}
