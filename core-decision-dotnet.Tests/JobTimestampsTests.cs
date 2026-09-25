namespace Photon.JobSeeker.Tests;

public class JobTimestampsTests
{
    private static readonly DateTime ScrapedAt = new(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);

    private const string IndeedJsonLd =
        """
        {"@context":"http://schema.org","@type":"JobPosting","datePosted":"2026-03-10T18:43:26.4Z","title":"Software Engineer","validThrough":"2027-04-10T18:43:26.4Z"}
        """;

    private const string LinkedInEscapedJsonLd =
        """
        <script type="application/ld+json">{"@type":"JobPosting","datePosted":"2026-09-16T01:34:25.605Z","employmentType":"FULL_TIME","url":"https:\u002F\u002Fwww.linkedin.com\u002Fjobs\u002Fview\u002F3920000000"}</script>
        """;

    private const string LinkedInNewUiMetadata =
        """
        <div class="_5e203799 fa3d9e2a" data-display-contents="true"><p class="a3b8ceaa _49454e33 cee5902c _9ed12745 _4facca12 dad557fd fdb4f4ca _6f900e9d _76917905 f9c2fb56"><span class="_6f900e9d fa3d9e2a">Amsterdam, North Holland, Netherlands</span><span class="_6e04d6e6 fa3d9e2a"> </span>·<span class="_6e04d6e6 fa3d9e2a"> </span><span class="_6f900e9d fa3d9e2a">2 months ago</span><span class="_6e04d6e6 fa3d9e2a"> </span>·<span class="_6e04d6e6 fa3d9e2a"> </span><span class="_6f900e9d fa3d9e2a">53 people clicked apply</span></p></div>
        """;

    private const string RepostedMetadata =
        """
        <div><p><span>Utrecht, Netherlands</span><span> </span>·<span> </span><span>Reposted 1 week ago</span><span> </span>·<span> </span><span>112 people clicked apply</span></p></div>
        """;

    private const string SimilarCardBadge =
        """
        <div><a href="https://www.linkedin.com/jobs/search-results?currentJobId=3920000000&amp;origin=SIMILAR_JOBS_CARD"><div class="bedbe1a6 _496fcd88 eebe61b8 _9e907aaf adfa979b bb90cad0 a19caacd fa3d9e2a"><p class="a3b8ceaa _88152668 cee5902c _9ed12745 dad557fd fdb4f4ca _6f900e9d _76917905 _49414cc6"><span class="ef783d36 fa3d9e2a">Posted 1 week ago</span><span aria-hidden="true">1 week ago</span></p></div></a></div>
        """;

    private const string TextLineTwoPart = "posted 8 minutes ago  ·  Over 100 applicants";

    private const string TextLineThreePart = "Amsterdam, North Holland, Netherlands · Reposted 1 week ago · 112 people clicked apply";

    [Fact]
    public void TryExtractExact_Parses_Indeed_JsonLd_With_Fractional_Seconds()
    {
        Assert.True(JobTimestamps.TryExtractExact(IndeedJsonLd, out var value));
        Assert.Equal(new DateTime(2026, 3, 10, 18, 43, 26, 400, DateTimeKind.Utc), value);
    }

    [Fact]
    public void TryExtractExact_Parses_Escaped_LinkedIn_JsonLd()
    {
        Assert.True(JobTimestamps.TryExtractExact(LinkedInEscapedJsonLd, out var value));
        Assert.Equal(new DateTime(2026, 9, 16, 1, 34, 25, 605, DateTimeKind.Utc), value);
    }

    [Fact]
    public void TryExtractExact_Anchors_On_DatePosted_Not_ValidThrough()
    {
        Assert.True(JobTimestamps.TryExtractExact(IndeedJsonLd, out var value));
        Assert.Equal(2026, value!.Value.Year);
        Assert.Equal(3, value.Value.Month);
    }

    [Fact]
    public void TryExtractExact_Malformed_Value_Returns_False_Without_Throwing()
    {
        const string html = """{"@type":"JobPosting","datePosted":"not-a-date"}""";

        Assert.False(JobTimestamps.TryExtractExact(html, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryExtractExact_Nothing_Found_Returns_False()
    {
        const string html = """<html><body><p>hello</p></body></html>""";

        Assert.False(JobTimestamps.TryExtractExact(html, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryExtractRelative_New_Ui_Metadata_Uses_Month_Midpoint()
    {
        Assert.True(JobTimestamps.TryExtractRelative(LinkedInNewUiMetadata, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddDays(-30), value);
    }

    [Fact]
    public void TryExtractRelative_Reposted_Prefix_Is_Still_The_Publication_Time()
    {
        Assert.True(JobTimestamps.TryExtractRelative(RepostedMetadata, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddDays(-3.5), value);
    }

    [Fact]
    public void TryExtractRelative_Text_Mode_Two_Part_Line()
    {
        Assert.True(JobTimestamps.TryExtractRelative(TextLineTwoPart, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddMinutes(-4), value);
    }

    [Fact]
    public void TryExtractRelative_Text_Mode_Three_Part_Line()
    {
        Assert.True(JobTimestamps.TryExtractRelative(TextLineThreePart, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddDays(-3.5), value);
    }

    [Fact]
    public void TryExtractRelative_Text_Mode_Skips_Lines_Without_Separator()
    {
        var content = "Senior Backend Developer\nThis was posted 3 days ago\nOver 100 applicants · 2 months ago · more";

        Assert.True(JobTimestamps.TryExtractRelative(content, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddDays(-30), value);
    }

    [Theory]
    [InlineData("1 week ago", -3.5)]
    [InlineData("2 months ago", -30)]
    [InlineData("15 hours ago", -7.5 / 24)]
    [InlineData("1 month ago", -15)]
    [InlineData("6 days ago", -3)]
    [InlineData("8 minutes ago", -4.0 / 1440)]
    public void TryExtractRelative_Midpoint_Math(string age, double expectedDays)
    {
        var content = $"Amsterdam · {age} · 53 people clicked apply";

        Assert.True(JobTimestamps.TryExtractRelative(content, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddDays(expectedDays), value);
    }

    [Theory]
    [InlineData("Just posted")]
    [InlineData("Today")]
    public void TryExtractRelative_Zero_Age_Tokens_Mean_Scraped_At(string token)
    {
        var content = $"Amsterdam · {token} · 53 applicants";

        Assert.True(JobTimestamps.TryExtractRelative(content, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt, value);
    }

    [Fact]
    public void TryExtractRelative_Similar_Card_Badge_Alone_Is_Not_Picked()
    {
        Assert.False(JobTimestamps.TryExtractRelative(SimilarCardBadge, ScrapedAt, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryExtractRelative_Main_Metadata_Wins_Over_Card_Badges()
    {
        var html = SimilarCardBadge + LinkedInNewUiMetadata + SimilarCardBadge;

        Assert.True(JobTimestamps.TryExtractRelative(html, ScrapedAt, out var value));
        Assert.Equal(ScrapedAt.AddDays(-30), value);
    }

    [Fact]
    public void TryExtractRelative_Skips_Paragraph_Containing_Anchor()
    {
        const string html =
            """
            <p>Read the <a href="https://example.com/blog">company blog</a> · 2 days ago</p>
            """;

        Assert.False(JobTimestamps.TryExtractRelative(html, ScrapedAt, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryExtractRelative_Bare_Age_Outside_Separator_Segments_Is_Ignored()
    {
        var content = "2 days ago we posted something\nAmsterdam, Netherlands · Over 100 applicants";

        Assert.False(JobTimestamps.TryExtractRelative(content, ScrapedAt, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryExtractRelative_Nothing_Found_Returns_False()
    {
        const string html = """<html><body><div>no dates here</div></body></html>""";

        Assert.False(JobTimestamps.TryExtractRelative(html, ScrapedAt, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Merge_Exact_Overwrites_Existing_And_Relative()
    {
        var current = new DateTime(2026, 1, 1);
        var exact = new DateTime(2026, 6, 15);
        var relative = new DateTime(2026, 7, 20);

        Assert.Equal(exact, JobTimestamps.Merge(current, exact, relative));
        Assert.Equal(exact, JobTimestamps.Merge(current, exact, null));
    }

    [Fact]
    public void Merge_Relative_Only_Fills_Null()
    {
        var current = new DateTime(2026, 1, 1);
        var relative = new DateTime(2026, 7, 20);

        Assert.Equal(relative, JobTimestamps.Merge(null, null, relative));
        Assert.Equal(current, JobTimestamps.Merge(current, null, relative));
    }

    [Fact]
    public void Merge_Null_Inputs_Keep_Current()
    {
        var current = new DateTime(2026, 1, 1);

        Assert.Equal(current, JobTimestamps.Merge(current, null, null));
        Assert.Null(JobTimestamps.Merge(null, null, null));
    }
}
