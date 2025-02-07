using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    public override string Name => "Indeed";

    public override string SearchLink => CurrentMethod.Url + "jobs?q=" + SearchTitle;

    public override Regex? JobAcceptabilityChecker => IndeedPage.reg_job_acceptability_checker;

    protected override void RunningSearchingMethodChanged(int value)
    {
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IndeedPage));
    }
}
