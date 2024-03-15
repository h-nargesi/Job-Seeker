using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn;

class LinkedIn : Agency
{
    private static readonly object @lock = new();

    private Location[] Locations { get; set; } = Array.Empty<Location>();


    public override string Name => "LinkedIn";

    public override Location[] SearchingMethodTitles => Locations;

    internal string RunningUrl => Locations[RunningSearchingMethodIndex].Url;


    public override string BaseUrl
    {
        get
        {
            var base_link = Link.Trim();
            return base_link.EndsWith('/') ? base_link[..^1] : base_link;
        }
    }
    public override string SearchLink => "https://www.linkedin.com/jobs/search/";

    public override Regex? JobAcceptabilityChecker => LinkedInPage.reg_job_no_longer_accepting;


    public override string GetMainHtml(string html) => LinkedInPageJob.GetHtmlContent(html);

    protected override void LoadSettings(dynamic? settings)
    {
        lock (@lock)
        {
            if (settings == null)
            {
                Locations = new Location[] { new() };
                RunningSearchingMethodIndex = 0;
            }
            else
            {
                var running = (int)settings!.running;
                if (RunningSearchingMethodIndex == running) return;

                Locations = settings.locations.ToObject<Location[]>();
                RunningSearchingMethodIndex = running;
            }
        }
    }

    protected override void RunningSearchingMethodChanged(int value)
    {
        var location = Uri.EscapeDataString(Locations[value].Url);

        LinkedInPage.reg_search_location_url = new Regex(
            @$"(^|&)location={location}(&|$)", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(LinkedInPage));
    }
}
