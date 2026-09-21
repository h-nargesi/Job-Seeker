using System.Web;
using Photon.JobSeeker.Pages;
using Serilog;

namespace Photon.JobSeeker.IamExpat;

class IamExpatPageJob : JobPage, IamExpatPage
{
    public IamExpatPageJob(IamExpat parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IamExpatPage.reg_job_url.IsMatch(url);
    }

    protected override string GetJobCode(string url)
    {
        var url_matched = IamExpatPage.reg_job_url.Match(url);
        if (!url_matched.Success) return string.Empty;

        return IamExpatPage.GetJobCode(url_matched);
    }

    protected override Command[]? JobFallow(string content)
    {
        return null;
    }

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        code = null;

        var apply_match = IamExpatPage.reg_job_apply.Match(html);
        apply = apply_match.Success ? HttpUtility.HtmlDecode(apply_match.Groups[1].Value) : null;
        if (apply == null)
            Log.Warning("Apply link not found ({0})", Parent.Name);

        var title_match = IamExpatPage.reg_job_title.Match(html);
        title = title_match.Success ? title_match.Groups[1].Value : null;
    }

    public override string GetHtmlContent(string html)
    {
        var start_match = IamExpatPage.reg_job_content_start.Match(html);
        if (!start_match.Success)
        {
            Log.Warning("Job content start marker not found ({0}), using full page", Parent.Name);
            return html;
        }

        var end_match = IamExpatPage.reg_job_content_end.Match(html, start_match.Index);
        if (!end_match.Success)
            end_match = IamExpatPage.reg_job_content_end_fallback.Match(html, start_match.Index);

        if (!end_match.Success)
        {
            Log.Warning("Job content end marker not found ({0}), using full page", Parent.Name);
            return html;
        }

        return html[start_match.Index..end_match.Index];
    }
}
