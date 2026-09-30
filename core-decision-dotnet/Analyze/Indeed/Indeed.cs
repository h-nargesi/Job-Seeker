using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    public override string Name => "Indeed";

    public override int DefaultWaiting => 6000;

    public override string BaseUrl => CurrentMethod.Url.TrimEnd('/');

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
