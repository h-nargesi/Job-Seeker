using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Stepstone;

class Stepstone : Agency
{
    public override string Name => "Stepstone";

    public override int Waiting => 5000;

    public override string SearchLink => $"{BaseUrl}/work/full-time/{SearchTitle}?ct=222&fdl=en";

    public override Regex? JobAcceptabilityChecker => null;

    protected override void RunningSearchingMethodChanged(int value)
    {
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(StepstonePage));
    }
}
