using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn;

class LinkedIn : Agency
{
    public override string Name => "LinkedIn";

    internal string RunningUrl => CurrentMethod.Url;

    public override string SearchLink => $"{BaseUrl}/jobs/search/";

    public override Regex? JobAcceptabilityChecker => LinkedInPage.reg_job_no_longer_accepting;

    protected override void RunningSearchingMethodChanged(int value)
    {
        var match = Regex.Match(RunningUrl, @"(?:^|&)(location|geoId)=([^&]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return;

        var parameter = match.Groups[1].Value.ToLowerInvariant();
        var location = Regex.Escape(Uri.EscapeDataString(Uri.UnescapeDataString(match.Groups[2].Value)));

        LinkedInPage.reg_search_location_url = new Regex(
            @$"(^|[?&]){parameter}={location}(&|$)", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(LinkedInPage));
    }
}
