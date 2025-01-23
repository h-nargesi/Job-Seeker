using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Bayt;

class Bayt : Agency
{
    public override string Name => "Bayt";

    public override string BaseUrl
    {
        get
        {
            var base_link = Link.Trim();
            return base_link.EndsWith('/') ? base_link[..^1] : base_link;
        }
    }

    public override string SearchLink => $"https://www.bayt.com/en/{CurrentMethod.Url}/jobs/{SearchTitle}-jobs/";

    public override Regex? JobAcceptabilityChecker => null;

    protected override void RunningSearchingMethodChanged(int value)
    {
        var location = Uri.EscapeDataString(CurrentMethod.Url);

        BaytPage.reg_search_location_url = new Regex(@$"/en/{location}/jobs", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(BaytPage));
    }
}
