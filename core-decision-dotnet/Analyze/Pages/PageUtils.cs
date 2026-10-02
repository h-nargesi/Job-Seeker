namespace Photon.JobSeeker.Pages;

public static class PageUtils
{
    public static string GetBaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            string.IsNullOrEmpty(uri.Host))
            return url;

        return $"{uri.Scheme}://{uri.Host}";
    }
}
