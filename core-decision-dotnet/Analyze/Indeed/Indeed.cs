﻿using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class Indeed : Agency
{
    private static readonly object @lock = new();

    private Location[] LocationDomains { get; set; } = Array.Empty<Location>();


    public override string Name => "Indeed";

    public override Location[] SearchingMethodTitles => LocationDomains;


    public override string BaseUrl
    {
        get
        {
            var base_link = LocationDomains[RunningSearchingMethodIndex].Url.Trim();
            return base_link.EndsWith('/') ? base_link[..^1] : base_link;
        }
    }

    public override string SearchLink => LocationDomains[RunningSearchingMethodIndex].Url + "jobs?q=" + SearchTitle;

    public override Regex? JobAcceptabilityChecker => IndeedPage.reg_job_acceptability_checker;


    public override string GetMainHtml(string html) => IndeedPageJob.GetHtmlContent(html);

    protected override void LoadSettings(dynamic? settings)
    {
        lock (@lock)
        {
            if (settings == null)
            {
                LocationDomains = new Location[] { new() };
                RunningSearchingMethodIndex = 0;
            }
            else
            {
                if (RunningSearchingMethodIndex == (int)settings.running) return;

                LocationDomains = settings.locations.ToObject<Location[]>();
                RunningSearchingMethodIndex = (int)settings.running;
            }
        }
    }

    protected override void RunningSearchingMethodChanged(int value)
    {
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IndeedPage));
    }
}
