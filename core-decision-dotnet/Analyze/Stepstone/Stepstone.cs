using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Stepstone;

class Stepstone : Agency
{
    public override string Name => "Stepstone";

    public override string SearchLink => $"{Link}/work/full-time/{SearchTitle}?ct=222&fdl=en";

    public override AgencyRegion ParseRegion(string url) => CurrentMethod;

    public override Regex? JobAcceptabilityChecker => null;

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(StepstonePage));
    }
}
