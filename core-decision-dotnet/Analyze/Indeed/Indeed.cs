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
            ["[data-testid='vj-job-title']"]),
        new ReadinessRule(IndeedPage.reg_search_url.ToString(),
            ["#mosaic-jobResults", "role='navigation'"]),
    ];

    protected override void RunningSearchingMethodChanged(int value)
    {
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IndeedPage));
    }
}
