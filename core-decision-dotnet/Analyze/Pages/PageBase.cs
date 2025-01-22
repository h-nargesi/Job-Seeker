using Photon.JobSeeker.Analyze.Pages;

namespace Photon.JobSeeker.Pages;

public abstract class PageBase : Page
{
    protected PageBase(Agency parent) : base(parent) { }

    protected abstract bool CheckInvalidUrl(string url, string content);
}