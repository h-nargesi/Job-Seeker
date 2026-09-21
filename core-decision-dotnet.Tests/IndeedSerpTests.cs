using Photon.JobSeeker.Indeed;

namespace Photon.JobSeeker.Tests;

public class IndeedSerpTests
{
    private const string SerpSnippet =
        """
        window.mosaic.providerData["mosaic-provider-jobcards-1"] = {
          metaData: {
            isIndeedPro: false,
            mosaicProviderJobCardsModel: {
              baseUrl: "https://nl.indeed.com",
              jobSeenLogParameters: {
                jobKeyToFeedTkMapForHomepage: {
                  "0e07a30c28494de3": "1k319iq0phabh800",
                  "96ace364606b9b9b": "1k319iq0phabh800",
                },
              },
              results: [
                {
                  jobkey: "0e07a30c28494de3",
                  displayTitle: "Tijdelijke CRA positie in Nederland",
                  company: "Syneos - Clinical and Corporate - Prod",
                  sponsored: true,
                  link: "/pagead/clk?mo=r&ad=-6NYlbfkN0CgLV01otWyadW_0bbvTXVl&xkcb=SoAq6_M2ehbEmpTCRx0EbzkdCdPP&jsa=8472&camk=C3EPSzFlQw9-8YWqhGvJHQ%3D%3D&from=hp.jobsForYou&tk=1k319iq0phabh800&vjs=3&mtk=1k319ipp3hc32800",
                  thirdPartyApplyUrl: "https://nl.indeed.com/applystart?jk=0e07a30c28494de3&from=homepage&mvj=0&jobsearchTk=1k319iq0phabh800&spon=1&adid=467111948",
                  viewJobLink:
                    "/viewjob?jk=0e07a30c28494de3&from=hp&tk=1k319iq0phabh800&viewtype=embedded&advn=6942524633806610&adid=467111948&ad=-6NYlbfkN0CgLV01otWyadW&xkcb=SoAq6_M2ehbEmpTCRx0EbzkdCdPP",
                },
                {
                  jobkey: "96ace364606b9b9b",
                  displayTitle: "PHP Maintenance Engineer | 2/3 dagen thuiswerk",
                  company: "MatchMatters BV",
                  sponsored: false,
                  link: "/rc/clk?jk=96ace364606b9b9b&from=hp.jobsForYou&tk=1k319iq0phabh800&bb=GyUdUPx6psFDdznv473d8k%3D%3D&xkcb=SoDV67M2ehbEmoTCRx0JbzkdCdPP&mtk=1k319ipp3hc32800",
                  viewJobLink:
                    "/viewjob?jk=96ace364606b9b9b&from=hp&tk=1k319iq0phabh800&viewtype=embedded&xkcb=SoDV67M2ehbEmoTCRx0JbzkdCdPP",
                },
              ],
            },
          },
        };
        """;

    [Fact]
    public void ExtractJobLinks_From_Mosaic_Jobcards_Prefers_ViewjobLink()
    {
        var links = IndeedSerp.ExtractJobLinks(SerpSnippet);

        Assert.Equal(
        new[]
        {
            ("/viewjob?jk=0e07a30c28494de3&from=hp&tk=1k319iq0phabh800&viewtype=embedded&advn=6942524633806610&adid=467111948&ad=-6NYlbfkN0CgLV01otWyadW&xkcb=SoAq6_M2ehbEmpTCRx0EbzkdCdPP", "0e07a30c28494de3"),
            ("/viewjob?jk=96ace364606b9b9b&from=hp&tk=1k319iq0phabh800&viewtype=embedded&xkcb=SoDV67M2ehbEmoTCRx0JbzkdCdPP", "96ace364606b9b9b"),
        }, links);
    }

    [Fact]
    public void ExtractJobLinks_Keeps_RcClk_Link_When_No_Viewjob_Exists()
    {
        const string html =
            """
            window.mosaic.providerData["mosaic-provider-jobcards-1"] = {
              results: [
                {
                  jobkey: "074b26363501302a",
                  displayTitle: "Recruitment Assistant - Italian Speaker (M/F/X)",
                  link: "/rc/clk?jk=074b26363501302a&from=hp.jobsForYou&tk=1k319iq0phabh800&bb=GyUdUPx6psFDdznv473d8n%3D%3D&xkcb=SoC567M2ehbEmozCRx0LbzkdCdPP&mtk=1k319ipp3hc32800",
                },
              ],
            };
            """;

        var links = IndeedSerp.ExtractJobLinks(html);

        Assert.Equal(
        new[]
        {
            ("/rc/clk?jk=074b26363501302a&from=hp.jobsForYou&tk=1k319iq0phabh800&bb=GyUdUPx6psFDdznv473d8n%3D%3D&xkcb=SoC567M2ehbEmozCRx0LbzkdCdPP&mtk=1k319ipp3hc32800", "074b26363501302a"),
        }, links);
    }

    [Fact]
    public void ExtractJobLinks_Dedupes_Repeated_Codes()
    {
        const string html =
            """
            <a href="/viewjob?jk=acd2093819ba6af0&from=hp&tk=1k319iq0phabh800&viewtype=embedded">Quality Coach</a>
            <a href="/m/viewjob?jk=acd2093819ba6af0&from=serpso&mobile=1">Quality Coach (mobile)</a>
            """;

        var links = IndeedSerp.ExtractJobLinks(html);

        Assert.Equal(
        new[] { ("/viewjob?jk=acd2093819ba6af0&from=hp&tk=1k319iq0phabh800&viewtype=embedded", "acd2093819ba6af0") },
        links);
    }

    [Fact]
    public void ExtractJobLinks_Decodes_Html_Escaped_Hrefs()
    {
        const string html =
            """
            <a href="/viewjob?jk=d735bb712daf5d3a&amp;from=hp&amp;tk=1k319iq0phabh800&amp;viewtype=embedded">PHP Maintenance Engineer</a>
            """;

        var links = IndeedSerp.ExtractJobLinks(html);

        Assert.Equal(
        new[] { ("/viewjob?jk=d735bb712daf5d3a&from=hp&tk=1k319iq0phabh800&viewtype=embedded", "d735bb712daf5d3a") },
        links);
    }

    [Fact]
    public void ExtractJobLinks_Ignores_Bare_Jobkeys_Without_Links()
    {
        const string html =
            """
            <div class="cardOutline" data-jk="abcdef12">Job A card</div>
            <script>window.mosaic = { results: [{ jobkey: "abcdef12", displayTitle: "Job A" }] };</script>
            """;

        Assert.Empty(IndeedSerp.ExtractJobLinks(html));
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
