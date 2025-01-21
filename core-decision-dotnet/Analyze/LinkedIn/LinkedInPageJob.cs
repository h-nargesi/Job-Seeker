using HtmlAgilityPack;
using Photon.JobSeeker.Pages;
using System.Web;

namespace Photon.JobSeeker.LinkedIn;

class LinkedInPageJob : JobPage, LinkedInPage
{
    public LinkedInPageJob(LinkedIn parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !LinkedInPage.reg_job_url.IsMatch(url);
    }

    protected override string GetJobCode(string url)
    {
        var url_matched = LinkedInPage.reg_job_url.Match(url);
        return url_matched.Success ? url_matched.Groups[1].Value : string.Empty;
    }

    protected override Command[]? JobFallow(string content)
    {
        if (!LinkedInPage.reg_job_adding.IsMatch(content)) return null;
        return new Command[] {
            Command.Click(@"button.jobs-save-button"),
            Command.Wait(3000),
        };
    }

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        code = null;
        apply = null;

        var title_match = LinkedInPage.reg_job_title.Match(html);
        title = title_match.Success ? HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim() : null;

    }

    protected override string GetHtmlContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var main_content = doc.DocumentNode.SelectNodes("//article")?
                                           .FirstOrDefault();

        if (main_content == null) return html;

        var title_content = doc.DocumentNode.SelectNodes("//div[contains(@class,'jobs-unified-top-card')]")?
                                            .FirstOrDefault();

        return string.Join("\n", title_content?.OuterHtml, main_content.OuterHtml);
    }
}