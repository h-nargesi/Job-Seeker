using System.Web;
using Photon.JobSeeker.Pages;

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
        if (!IamExpatPage.reg_job_adding.IsMatch(content)) return null;
        return new Command[] {
            Command.Click(@"a[rel=""nofollow""]"),
            Command.Wait(3000)
        };
    }

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        var code_matched = IamExpatPage.reg_job_shortlink.Match(html);
        code = code_matched.Success ? code_matched.Groups[1].Value : null;

        var apply_match = IamExpatPage.reg_job_apply.Match(html);
        apply = apply_match.Success ? apply_match.Groups[1].Value : null;

        var title_match = IamExpatPage.reg_job_title.Match(html);
        title = title_match.Success ? HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim() : null;
    }

    protected override string GetHtmlContent(string html)
    {
        var start_match = IamExpatPage.reg_job_content_start.Match(html);
        if (!start_match.Success) return html;

        var end_match = IamExpatPage.reg_job_content_end.Match(html);
        if (!end_match.Success) return html;

        html = html[start_match.Index..(end_match.Index + end_match.Length)];

        start_match = IamExpatPage.reg_job_content_apply_start.Match(html);
        if (!start_match.Success) return html;

        end_match = IamExpatPage.reg_job_content_apply_end.Match(html);
        if (!end_match.Success) return html;

        return html.Remove(start_match.Index, end_match.Index + end_match.Length - start_match.Index);
    }
}