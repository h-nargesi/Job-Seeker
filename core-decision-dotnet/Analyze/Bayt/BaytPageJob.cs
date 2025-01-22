using HtmlAgilityPack;
using Photon.JobSeeker.Pages;
using System.Web;

namespace Photon.JobSeeker.Bayt;

class BaytPageJob(Bayt parent) : JobPage(parent), BaytPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !BaytPage.reg_job_view.IsMatch(url);
    }

    protected override string GetJobCode(string url)
    {
        var url_matched = BaytPage.reg_job_view.Match(url);
        return url_matched.Success ? url_matched.Groups[1].Value : string.Empty;
    }

    protected override Command[]? JobFallow(string content)
    {
        return null;
    }

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        code = null;
        apply = null;

        var title_match = BaytPage.reg_job_title.Match(html);
        title = title_match.Success ? HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim() : null;
    }

    public override string GetHtmlContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var main_content = doc.DocumentNode.SelectNodes("//div[contains(@id,'job_card')]")?
                                           .FirstOrDefault();

        return main_content?.OuterHtml ?? html;
    }
}
