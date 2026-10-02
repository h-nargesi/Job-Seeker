using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    public override string Name => "Indeed";

    public override string SearchLink => CurrentMethod.Url + "jobs?q=" + SearchTitle;

    public override Regex? JobAcceptabilityChecker => IndeedPage.reg_job_acceptability_checker;

    public override ReadinessRule[] ReadinessRules =>
    [
        new ReadinessRule(IndeedPage.reg_job_view.ToString(),
            ["[data-testid='vj-job-title']"]),
        new ReadinessRule(IndeedPage.reg_search_url.ToString(),
            ["#mosaic-jobResults", "role='navigation'"]),
    ];

    public override AgencyRegion ParseRegion(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return AgencyRegion.Empty;

        foreach (var method in AllSearchingMethod)
        {
            if (Uri.TryCreate(method.Url, UriKind.Absolute, out var method_uri) &&
                string.Equals(method_uri.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
                return method;
        }

        return AgencyRegion.Empty;
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IndeedPage));
    }
}
