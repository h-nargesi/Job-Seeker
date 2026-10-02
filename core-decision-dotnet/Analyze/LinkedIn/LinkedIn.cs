using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn;

class LinkedIn : Agency
{
    public override string Name => "LinkedIn";

    public override string SearchLink => $"{Link}/jobs/search/";

    public override Regex? JobAcceptabilityChecker => LinkedInPage.reg_job_no_longer_accepting;

    public override AgencyRegion ParseRegion(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return AgencyRegion.Empty;

        foreach (var method in AllSearchingMethod)
        {
            var pattern = LinkedInPage.GetSearchLocationUrlPattern(method.Url);
            if (pattern == null) continue;

            var value = Regex.Match(uri.Query,
                @$"(^|[?&]){pattern.Value.parameter}={pattern.Value.location}(&|$)", RegexOptions.IgnoreCase);
            if (value.Success) return method;
        }

        return AgencyRegion.Empty;
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(LinkedInPage));
    }
}
