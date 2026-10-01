using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    public override string Name => "Indeed";

    public override string BaseUrl => CurrentMethod.Url.TrimEnd('/');

    public override string SearchLink => CurrentMethod.Url + "jobs?q=" + SearchTitle;

    public override Regex? JobAcceptabilityChecker => IndeedPage.reg_job_acceptability_checker;

    public override ReadinessRule[] ReadinessRules =>
    [
        new ReadinessRule(IndeedPage.reg_job_view.ToString(),
            ["div.jobsearch-ViewJobLayout-jobDisplay"]),
        new ReadinessRule(IndeedPage.reg_search_url.ToString(),
            ["div.jobsearch-ResultsList", "#resultsCol"]),
    ];

    protected override void RunningSearchingMethodChanged(int value)
    {
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IndeedPage));
    }
}
