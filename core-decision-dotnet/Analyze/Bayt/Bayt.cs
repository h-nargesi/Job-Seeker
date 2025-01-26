using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Bayt;

class Bayt : Agency
{
    public override string Name => "Bayt";

    public override string SearchLink => $"{BaseUrl}/en/{CurrentMethod.Url}/jobs/{SearchTitle}-jobs/";

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
