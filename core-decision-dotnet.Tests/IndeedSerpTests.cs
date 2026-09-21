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
    public void ExtractJobLinks_From_Mosaic_Jobcards_Builds_Canonical_Viewjob_Urls()
    {
        var links = IndeedSerp.ExtractJobLinks(SerpSnippet);

        Assert.Equal(
        new[]
        {
            ("/viewjob?jk=0e07a30c28494de3", "0e07a30c28494de3"),
            ("/viewjob?jk=96ace364606b9b9b", "96ace364606b9b9b"),
        }, links);
    }

    [Fact]
    public void ExtractJobLinks_Converts_RcClk_Link_To_Canonical_Viewjob()
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
            ("/viewjob?jk=074b26363501302a", "074b26363501302a"),
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
        new[] { ("/viewjob?jk=acd2093819ba6af0", "acd2093819ba6af0") },
        links);
    }

    [Fact]
    public void ExtractJobLinks_Matches_Html_Escaped_Hrefs()
    {
        const string html =
            """
            <a href="/viewjob?jk=d735bb712daf5d3a&amp;from=hp&amp;tk=1k319iq0phabh800&amp;viewtype=embedded">PHP Maintenance Engineer</a>
            """;

        var links = IndeedSerp.ExtractJobLinks(html);

        Assert.Equal(
        new[] { ("/viewjob?jk=d735bb712daf5d3a", "d735bb712daf5d3a") },
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

    [Fact]
    public void NextPageLink_Prefers_Link_Rel_Next_From_Head()
    {
        const string html =
            """
            <head>
              <link rel="preconnect" href="https://js.dtxhq.jp">
              <link rel="next" href="/jobs?q=developer&amp;l=&amp;forceLocation=-1&amp;start=10">
              <link rel="canonical" href="https://nl.indeed.com/q-developer-vacatures.html">
            </head>
            """;

        Assert.Equal("/jobs?q=developer&l=&forceLocation=-1&start=10", IndeedSerp.FindNextPageLink(html));
    }

    [Fact]
    public void NextPageLink_Ignores_Href_Anchor_And_Other_Rels()
    {
        const string html =
            """
            <link rel="canonical" href="https://nl.indeed.com/q-developer-vacatures.html">
            <link rel="alternate" href="android-app://com.indeed.android.jobsearch/https/nl.indeed.com/m/jobs?q=developer">
            <link rel="next" href="#">
            """;

        Assert.Null(IndeedSerp.FindNextPageLink(html));
    }

    [Fact]
    public void NextPageSelector_Matches_Dutch_Localized_Pagination_Anchor()
    {
        const string html =
            """
            <li class="serp-page-8umzvb eu4oa1w0"><a data-testid="pagination-page-next" aria-label="Volgende pagina" href="/jobs?q=developer&amp;start=10" class="serp-page-1v7ptvg e71d0lh0"><svg aria-hidden="true"></svg></a></li>
            """;

        Assert.Equal(IndeedSerp.next_page_selector, IndeedSerp.FindNextPageSelector(html));
    }

    [Fact]
    public void NextPageSelector_Matches_Renamed_Next_Testid()
    {
        const string html =
            """
            <a data-testid="pagination-next-page" aria-label="Volgende" href="/jobs?q=developer&amp;start=10">Volgende</a>
            """;

        Assert.Equal(IndeedSerp.next_page_selector, IndeedSerp.FindNextPageSelector(html));
    }

    [Fact]
    public void NextPageSelector_Matches_English_Aria_Label_Fallback()
    {
        const string html =
            """
            <a aria-label="Next Page" href="/jobs?q=developer&amp;start=10">Next</a>
            """;

        Assert.Equal(IndeedSerp.next_page_selector, IndeedSerp.FindNextPageSelector(html));
    }

    [Fact]
    public void NextPageSelector_Uses_Localized_Label_From_Translations_When_Testid_Missing()
    {
        const string html =
            """
            <script>window._translations = {"Moved to Offered":[null,"Verplaatst"],"Next Page":[null,"Volgende pagina"],"Next page":[null,"Volgende pagina"]};</script>
            <ul><li><a aria-label="Volgende pagina" href="/jobs?q=developer&amp;start=10"><svg></svg></a></li></ul>
            """;

        Assert.Equal(@"a[aria-label=""Volgende pagina""]", IndeedSerp.FindNextPageSelector(html));
    }

    [Fact]
    public void NextPageSelector_Null_On_Last_Page_Without_Next_Anchor()
    {
        const string html =
            """
            <script>window._translations = {"Next Page":[null,"Volgende pagina"]};</script>
            <nav>
              <a data-testid="pagination-page-current" aria-current="page" href="#">1</a>
              <a data-testid="pagination-page-2" aria-label="2" href="/jobs?q=developer&amp;start=10">2</a>
              <a aria-label="Page 3" href="/jobs?q=developer&amp;start=20">3</a>
            </nav>
            """;

        Assert.Null(IndeedSerp.FindNextPageSelector(html));
    }
}
