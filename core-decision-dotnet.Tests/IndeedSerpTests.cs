using Photon.JobSeeker.Indeed;

namespace Photon.JobSeeker.Tests;

public class IndeedSerpTests
{
    private const string SerpSnippet =
        """
        <div class="jobsearch-ResultsList">
          <a href="/rc/clk?jk=aaaaaaaa&amp;fccid=8c2f1f0e0d1b2a3f">Senior .NET Developer</a>
          <a href="/rc/clk?jk=bbbbbbbb&amp;fccid=8c2f1f0e0d1b2a3f">Backend Engineer</a>
          <a href="/viewjob?jk=bbbbbbbb&amp;from=serpso&amp;tk=1a2b3c4d">Backend Engineer</a>
          <a href="/m/viewjob?jk=cccccccc">Fullstack Developer</a>
          <div class="cardOutline" data-jk="eeeeeeee">DevOps Engineer</div>
        </div>
        """;

    [Fact]
    public void ExtractJobLinks_Returns_Decoded_Anchor_Hrefs_With_Viewjob_Preference()
    {
        var links = IndeedSerp.ExtractJobLinks(SerpSnippet);

        Assert.Equal(
        new[]
        {
            ("/viewjob?jk=bbbbbbbb&from=serpso&tk=1a2b3c4d", "bbbbbbbb"),
            ("/m/viewjob?jk=cccccccc", "cccccccc"),
            ("/rc/clk?jk=aaaaaaaa&fccid=8c2f1f0e0d1b2a3f", "aaaaaaaa"),
        }, links);
    }

    [Fact]
    public void ExtractJobLinks_Ignores_DataJk_Only_Cards()
    {
        const string html =
            """
            <div class="cardOutline" data-jk="abcdef12">Job A card</div>
            """;

        Assert.Empty(IndeedSerp.ExtractJobLinks(html));
    }

    [Fact]
    public void ExtractJobLinks_Dedupes_Repeated_Codes()
    {
        const string html =
            """
            <a href="/viewjob?jk=abcdef12">Job A</a>
            <a href="/m/viewjob?jk=abcdef12&amp;mobile=1">Job A again</a>
            """;

        var links = IndeedSerp.ExtractJobLinks(html);

        Assert.Equal(new[] { ("/viewjob?jk=abcdef12", "abcdef12") }, links);
    }

    [Fact]
    public void ExtractJobLinks_Empty_On_Unrelated_Html()
    {
        const string html =
            """
            <html><body>
              <a href="/company/Photon">Company page</a>
              <a href="/jobs?q=net">Search again</a>
            </body></html>
            """;

        Assert.Empty(IndeedSerp.ExtractJobLinks(html));
    }
}
