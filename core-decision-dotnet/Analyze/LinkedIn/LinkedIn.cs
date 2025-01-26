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
        var location = Uri.EscapeDataString(RunningUrl);

        LinkedInPage.reg_search_location_url = new Regex(
            @$"(^|&)location={location}(&|$)", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(LinkedInPage));
    }
}
