using HtmlAgilityPack;
using Photon.JobSeeker.Pages;
using System.Web;

namespace Photon.JobSeeker.Stepstone;

class StepstonePageJob(Stepstone parent) : JobPage(parent), StepstonePage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !StepstonePage.reg_job_url.IsMatch(url);
    }

    protected override string GetJobCode(string url)
    {
        var url_matched = StepstonePage.reg_job_url.Match(url);
        return url_matched.Success ? url_matched.Groups[1].Value : string.Empty;
    }

    protected override Command[]? JobFallow(string content)
    {
        if (!StepstonePage.reg_job_adding.IsMatch(content)) return null;

        return
        [
            Command.Click(@"button.jobs-save-button"),
            Command.Wait(3000),
        ];
    }

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        code = null;
        apply = null;

        var title_match = StepstonePage.reg_job_title.Match(html);
        title = title_match.Success ? HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim() : null;
    }

    public override string GetHtmlContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var main_content = doc.DocumentNode.SelectNodes("//div[contains(@class,'reb-main')]")?
                                           .FirstOrDefault();

        return main_content?.OuterHtml ?? html;
    }
}
