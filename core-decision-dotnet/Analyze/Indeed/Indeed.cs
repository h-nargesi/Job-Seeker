using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    public override string Name => "Indeed";

    public override string BaseUrl => CurrentMethod.Url.TrimEnd('/');

    public override int DefaultWaiting => 16000;

    public override string NormalizeJobUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;

        var host = new Uri(BaseUrl).Host;
        return uri.Host == host ? url : $"https://{host}{uri.PathAndQuery}";
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
