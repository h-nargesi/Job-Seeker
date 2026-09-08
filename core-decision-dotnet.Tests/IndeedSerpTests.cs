using Photon.JobSeeker.Indeed;

namespace Photon.JobSeeker.Tests;

public class IndeedSerpTests
{
    private const string SerpSnippet =
        """
        <div class="jobsearch-ResultsList">
          <a href="/rc/clk?jk=aaaaaaaa&fccid=8c2f1f0e0d1b2a3f">Senior .NET Developer</a>
          <a href="/viewjob?jk=bbbbbbbb&from=serpso&tk=1a2b3c4d">Backend Engineer</a>
          <a href="/m/viewjob?jk=cccccccc">Fullstack Developer</a>
          <div class="cardOutline" data-jk="dddddddd">DevOps Engineer</div>
        </div>
        """;

    [Fact]
    public void ExtractJobCodes_Returns_All_Link_Forms()
    {
        var codes = IndeedSerp.ExtractJobCodes(SerpSnippet);

        Assert.Equal(new[] { "aaaaaaaa", "bbbbbbbb", "cccccccc", "dddddddd" }, codes);
    }

    [Fact]
    public void ExtractJobCodes_Dedupes_Repeated_Codes()
    {
        const string html =
            """
            <a href="/rc/clk?jk=abcdef12">Job A</a>
            <div data-jk="abcdef12">Job A card</div>
            """;

        var codes = IndeedSerp.ExtractJobCodes(html);

        Assert.Equal(new[] { "abcdef12" }, codes);
    }

    [Fact]
    public void ExtractJobCodes_Empty_On_Unrelated_Html()
    {
        const string html =
            """
            <html><body>
              <a href="/company/Photon">Company page</a>
              <a href="/jobs?q=net">Search again</a>
            </body></html>
            """;

        Assert.Empty(IndeedSerp.ExtractJobCodes(html));
    }
}
