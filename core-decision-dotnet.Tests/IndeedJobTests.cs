using Photon.JobSeeker.Indeed;

namespace Photon.JobSeeker.Tests;

public class IndeedJobTests
{
    private const string JobPageSnippet =
        """
        <head><meta content="V.I.E Software developer (M/F)" id="indeed-share-message"><meta content="simple" id="indeed-share-type"></head>
        <body><div data-testid="desktop-job-header">
          <h5 dir="ltr" aria-level="5" role="heading" class="css-146c3p1 r-1xnzce8" data-testid="vj-job-title" style="font-weight: bold;">V.I.E Software developer (M/F)</h5>
        </div></body>
        """;

    [Fact]
    public void ExtractTitle_Prefers_VjJobTitle_Heading()
    {
        Assert.Equal("V.I.E Software developer (M/F)", IndeedJob.ExtractTitle(JobPageSnippet));
    }

    [Fact]
    public void ExtractTitle_From_Heading_With_Real_Dom_Shape()
    {
        const string html =
            """
            <div class="css-g5y9jx" data-testid="company-info-title-row"><h5 dir="ltr" aria-level="5" role="heading" class="css-146c3p1 r-1xnzce8" data-testid="vj-job-title" style="font-family: &quot;Indeed Sans&quot;;">Mandatory Internship Full-Stack Development for Release Automation of Automotive Embedded Middleware Software</h5></div>
            """;

        Assert.Equal(
            "Mandatory Internship Full-Stack Development for Release Automation of Automotive Embedded Middleware Software",
            IndeedJob.ExtractTitle(html));
    }

    [Fact]
    public void ExtractTitle_Falls_Back_To_ShareMessage_Meta()
    {
        const string html =
            """
            <head>
              <meta content="Apply for the Software Engineer job in Berlin!" name="description">
              <meta content="Software Engineer" id="indeed-share-message">
              <meta content="https://www.indeed.com/viewjob?jk=ba437d6670eb533d" id="indeed-share-url">
            </head>
            """;

        Assert.Equal("Software Engineer", IndeedJob.ExtractTitle(html));
    }

    [Fact]
    public void ExtractTitle_Falls_Back_To_Legacy_H1()
    {
        const string plain =
            """
            <h3 class="icl-JobTitle">Senior .NET Developer</h3>
            """;

        const string with_span =
            """
            <h1 class="jobsearch-JobInfoHeader-title">Job title: <span>Backend Engineer</span></h1>
            """;

        Assert.Null(IndeedJob.ExtractTitle(plain));
        Assert.Equal("Backend Engineer", IndeedJob.ExtractTitle(with_span));
    }

    [Fact]
    public void ExtractTitle_Decodes_Html_Entities()
    {
        const string html =
            """
            <h5 data-testid="vj-job-title">R&amp;D Engineer &#8211; Platform</h5>
            """;

        Assert.Equal("R&D Engineer – Platform", IndeedJob.ExtractTitle(html));
    }

    [Fact]
    public void ExtractTitle_Skips_Empty_Heading_And_Uses_Meta()
    {
        const string html =
            """
            <h5 data-testid="vj-job-title"></h5><meta content="DevOps Engineer" id="indeed-share-message">
            """;

        Assert.Equal("DevOps Engineer", IndeedJob.ExtractTitle(html));
    }

    [Fact]
    public void ExtractTitle_Null_When_No_Title_Present()
    {
        const string html =
            """
            <html><body><h2>Oops</h2><div>Sivua ei löydy</div></body></html>
            """;

        Assert.Null(IndeedJob.ExtractTitle(html));
    }
}
