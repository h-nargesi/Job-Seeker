﻿using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Stepstone;

class Stepstone : Agency
{
    public override string Name => "Stepstone";

    public override int Waiting => 5000;

    public override string BaseUrl
    {
        get
        {
            var base_link = Link.Trim();
            return base_link.EndsWith('/') ? base_link[..^1] : base_link;
        }
    }

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
