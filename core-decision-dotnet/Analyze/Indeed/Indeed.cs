using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    public override string Name => "Indeed";


    public override string BaseUrl
    {
        get
        {
            var base_link = CurrentMethod.Url.Trim();
            return base_link.EndsWith('/') ? base_link[..^1] : base_link;
        }
    }

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
