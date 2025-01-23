using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat;

class IamExpat : Agency
{
    public override string Name => "IamExpat";

    public override string BaseUrl => "https://iamexpat." + CurrentMethod.Url[0..2];

    public override string SearchLink => "https://iamexpat." + CurrentMethod.Url;

    public override Regex? JobAcceptabilityChecker => null;

    protected override void RunningSearchingMethodChanged(int value)
    {
        var location = CurrentMethod.Url.Replace(@"\", @"\\").Replace(@".", @"\.");

        IamExpatPage.reg_search_url = new Regex(
            @$"://[^/]*iamexpat\.{location}", RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IamExpatPage));
    }
}
