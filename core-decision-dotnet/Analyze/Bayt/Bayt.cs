using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Bayt;

class Bayt : Agency
{
    public override string Name => "Bayt";

    public override string SearchLink => $"{Link}/en/{CurrentMethod.Url}/jobs/{SearchTitle}-jobs/";

    public override AgencyRegion ParseRegion(string url)
    {
        foreach (var method in AllSearchingMethod)
        {
            if (Regex.IsMatch(url, $@"/en/{method.Url}/jobs", RegexOptions.IgnoreCase))
                return method;
        }

        return AgencyRegion.Empty;
    }

    public override Regex? JobAcceptabilityChecker => null;

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(BaytPage));
    }
}
