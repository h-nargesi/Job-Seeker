using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn;

class LinkedIn : Agency
{
    public override string Name => "LinkedIn";

    internal string RunningUrl => CurrentMethod.Url;

    public override string BaseUrl
    {
        get
        {
            var base_link = Link.Trim();
            return base_link.EndsWith('/') ? base_link[..^1] : base_link;
        }
    }

    public override string SearchLink => "https://www.linkedin.com/jobs/search/";

    public override Regex? JobAcceptabilityChecker => LinkedInPage.reg_job_no_longer_accepting;

    protected override void RunningSearchingMethodChanged(int value)
    {
        var location = Uri.EscapeDataString(RunningUrl);

        LinkedInPage.reg_search_location_url = new Regex(
            @$"(^|&)location={location}(&|$)", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(LinkedInPage));
    }
}
