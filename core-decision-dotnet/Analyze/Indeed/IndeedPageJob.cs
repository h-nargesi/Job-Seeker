using System.Web;
using HtmlAgilityPack;
using Photon.JobSeeker.IamExpat;
using Photon.JobSeeker.Pages;
using Serilog;

namespace Photon.JobSeeker.Indeed;

class IndeedPageJob : JobPage, IndeedPage
{
    public IndeedPageJob(Indeed parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content, out Command[]? commands)
    {
        commands = null;
        return !IndeedPage.reg_job_view.IsMatch(url);
    }

    protected override string GetJobCode(string text)
    {
        var url_matched = IndeedPage.reg_job_view.Match(text);
        if (!url_matched.Success) return string.Empty;

        return url_matched.Groups[1].Value;
    }

    protected override bool JobFallow(string text, out Command[] commands)
    {
        commands = new Command[]
        {
            Command.Click(@"button.jobs-save-button"),
            Command.Wait(3000),
        };

        return IndeedPage.reg_job_adding.IsMatch(text);
    }

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        apply = null;
        code = null;

        var title_match = IndeedPage.reg_job_title.Match(html);
        title = title_match.Success ? HttpUtility.HtmlDecode(title_match.Groups[2].Value).Trim() : null;
    }

    protected override void ChceckJob(Job job)
    {
        if (job.Content?.Contains("Indeed does not provide services in your region") == true)
            throw new Exception("Indeed does not provide services in your region.");
    }

    protected override string GetHtmlContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var main_content = doc.DocumentNode.SelectNodes("//div[contains(@class,'jobsearch-ViewJobLayout-jobDisplay')]")?
                                            .FirstOrDefault();

        return main_content?.OuterHtml ?? html;
    }
}
