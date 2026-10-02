using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat;

class IamExpat : Agency
{
    private string base_link = string.Empty;

    public override string Name => "IamExpat";

    public override string SearchLink
    {
        get
        {
            if (string.IsNullOrEmpty(base_link))
            {
                var index = Link.LastIndexOf('.');
                base_link = index < 0 ? Link : Link[..(index + 1)];
            }

            return base_link + CurrentMethod.Url;
        }
    }

    public override Regex? JobAcceptabilityChecker => null;

    public override AgencyRegion ParseRegion(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return AgencyRegion.Empty;

        foreach (var method in AllSearchingMethod)
        {
            if (method.Url.Length < 3) continue;

            var tld = Regex.Escape(method.Url[0..2]);
            if (Regex.IsMatch(uri.Host, @$"iamexpat\.{tld}$", RegexOptions.IgnoreCase))
                return method;
        }

        return AgencyRegion.Empty;
    }

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IamExpatPage));
    }
}
