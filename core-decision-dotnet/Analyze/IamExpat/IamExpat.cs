using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat;

class IamExpat : Agency
{
    private static readonly object @lock = new();

    private Location[] LocationUrls { get; set; } = Array.Empty<Location>();


    public override string Name => "IamExpat";

    public override Location[] SearchingMethodTitles => LocationUrls;


    public override string BaseUrl => "https://iamexpat." + LocationUrls[RunningSearchingMethodIndex].Url[0..2];

    public override string SearchLink => "https://iamexpat." + LocationUrls[RunningSearchingMethodIndex].Url;

    public override Regex? JobAcceptabilityChecker => null;


    public override string GetMainHtml(string html) => IamExpatPageJob.GetHtmlContent(html);

    protected override void LoadSettings(dynamic? settings)
    {
        lock (@lock)
        {
            if (settings == null)
            {
                LocationUrls = new Location[] { new() };
                RunningSearchingMethodIndex = 0;
            }
            else
            {
                var running = (int)settings!.running;
                if (RunningSearchingMethodIndex == running) return;

                LocationUrls = settings.locations.ToObject<Location[]>();
                RunningSearchingMethodIndex = running;
            }
        }
    }

    protected override void RunningSearchingMethodChanged(int value)
    {
        var location = LocationUrls[value].Url.Replace(@"\", @"\\")
                                              .Replace(@".", @"\.");

        IamExpatPage.reg_search_url = new Regex(
            @$"://[^/]*iamexpat\.{location}", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IamExpatPage));
    }
}
