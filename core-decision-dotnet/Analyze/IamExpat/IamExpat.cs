using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat;

class IamExpat : Agency
{
    private string base_link = string.Empty;
    
    public override string Name => "IamExpat";

    public override string BaseUrl => base_link + CurrentMethod.Url[0..2];

    public override string SearchLink => base_link + CurrentMethod.Url;

    public override Regex? JobAcceptabilityChecker => null;

    protected override void RunningSearchingMethodChanged(int value)
    {
        var location = CurrentMethod.Url.Replace(@"\", @"\\").Replace(@".", @"\.");

        IamExpatPage.reg_search_url = new Regex(
            @$"://[^/]*iamexpat\.{location}", RegexOptions.IgnoreCase);

        if (string.IsNullOrEmpty(base_link))
        {
            var index = Link.LastIndexOf('.');
            base_link = index < 0 ? Link : Link[..(index + 1)];
        }
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IamExpatPage));
    }
}
