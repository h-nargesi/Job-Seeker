using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Glassdoor;

class Glassdoor : Agency
{
    public override string Name => "Glassdoor";

    public override string SearchLink
    {
        get
        {
            var il = $"IL.0,{CurrentMethod.Url.Length}";
            var keyword_index = CurrentMethod.Url.Length + 1;
            var ko = $"KO.{keyword_index},{keyword_index + SearchTitle.Length}";

            return $"{BaseUrl}/{CurrentMethod.Url}-{SearchTitle}-jobs-SRCH_{il}_{CurrentMethod.Params}_{ko}.htm";
        }
    }

    public override Regex? JobAcceptabilityChecker => null;

    protected override void RunningSearchingMethodChanged(int value)
    {
        //var location = Uri.EscapeDataString(CurrentMethod.Url);

        //BaytPage.reg_search_location_url = new Regex(@$"/en/{location}/jobs", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        //return TypeHelper.GetSubTypes(typeof(BaytPage));
        return null;
    }
}

/*
https://www.glassdoor.com/Job/qatar-developer-jobs-SRCH_IL.0,5_IN199_KO6,15.htm
https://www.glassdoor.com/Job/oman-developer-jobs-SRCH_IL.0,4_IN167_KO5,14.htm
*/