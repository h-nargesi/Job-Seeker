namespace Photon.JobSeeker.Tests;

public class JobEligibilityHelperTests
{
    [Fact]
    public void LanguageIsMatch_counts_distinct_words_only()
    {
        using var fixture = new EligibilityFixture(["alpha", "beta"]);

        var job = EligibilityFixture.MakeJob("alpha Alpha ALPHA beta");

        Assert.True(fixture.Helper.LanguageIsMatch(job));
        Assert.Contains("English: (100%)", job.Log);
    }

    [Fact]
    public void LanguageIsMatch_false_for_persian_only_content()
    {
        using var fixture = new EligibilityFixture();

        var job = EligibilityFixture.MakeJob("سلام دنیا این یک متن فارسی است");

        Assert.False(fixture.Helper.LanguageIsMatch(job));
        Assert.Null(job.Log);
    }

    [Theory]
    [InlineData("")]
    [InlineData(":: 1234 -- ()")]
    public void LanguageIsMatch_false_without_three_letter_words(string content)
    {
        using var fixture = new EligibilityFixture(["alpha"]);

        Assert.False(fixture.Helper.LanguageIsMatch(EligibilityFixture.MakeJob(content)));
    }

    [Fact]
    public void LanguageIsMatch_false_for_null_content()
    {
        using var fixture = new EligibilityFixture(["alpha"]);

        Assert.False(fixture.Helper.LanguageIsMatch(EligibilityFixture.MakeJob(null)));
    }

    [Fact]
    public void LanguageIsMatch_boundary_fifty_percent_passes()
    {
        using var fixture = new EligibilityFixture(["alpha"]);

        var job = EligibilityFixture.MakeJob("alpha beta");

        Assert.True(fixture.Helper.LanguageIsMatch(job));
        Assert.Contains("English: (50%)", job.Log);
    }

    [Fact]
    public void LanguageIsMatch_below_fifty_percent_fails()
    {
        using var fixture = new EligibilityFixture(["alpha"]);

        var job = EligibilityFixture.MakeJob("alpha beta gamma");

        Assert.False(fixture.Helper.LanguageIsMatch(job));
        Assert.Contains("English: (33%)", job.Log);
    }

    [Fact]
    public void LanguageIsMatch_sums_english_count_across_word_batches()
    {
        var words = EligibilityFixture.GenerateWords(250);
        using var fixture = new EligibilityFixture(words);

        var job = EligibilityFixture.MakeJob(string.Join(" ", words));

        Assert.True(fixture.Helper.LanguageIsMatch(job));
        Assert.Contains("English: (100%)", job.Log);
    }

    [Fact]
    public void EvaluateEligibility_requires_a_field_match_even_with_high_score()
    {
        using var fixture = new EligibilityFixture(null,
            EligibilityFixture.Option("skill", 200, "sql", "Sql"));

        var job = EligibilityFixture.MakeJob("sql server everywhere");

        Assert.False(fixture.Helper.EvaluateEligibility(job, out var rejected));
        Assert.False(rejected);
        Assert.Equal(200L, job.Score);
    }

    [Fact]
    public void EvaluateEligibility_reject_option_sets_rejected_and_skips_scoring()
    {
        using var fixture = new EligibilityFixture(null,
            EligibilityFixture.Option("field", 100, "backend", "Backend"),
            EligibilityFixture.Option("reject", 1, "commute", "Commute"));

        var job = EligibilityFixture.MakeJob("backend with commute");

        Assert.False(fixture.Helper.EvaluateEligibility(job, out var rejected));
        Assert.True(rejected);
        Assert.Equal(100L, job.Score);
        Assert.Contains("*Field:*", job.Log);
        Assert.DoesNotContain("*Reject:*", job.Log);
    }

    [Fact]
    public void EvaluateEligibility_field_plus_score_at_threshold_passes()
    {
        using var fixture = new EligibilityFixture(null,
            EligibilityFixture.Option("field", 60, "backend", "Backend"),
            EligibilityFixture.Option("skill", 60, "sql", "Sql"));

        var job = EligibilityFixture.MakeJob("backend with sql", "Dev");

        Assert.True(fixture.Helper.EvaluateEligibility(job, out var rejected));
        Assert.False(rejected);
        Assert.Equal(120L, job.Score);
        Assert.Contains("*Field:*", job.Log);
        Assert.Contains("*Skill:*", job.Log);
        Assert.Contains("backend", job.Log);
        Assert.Contains("sql", job.Log);
        Assert.NotNull(job.Options);
        Assert.True(job.Options.Keys.ContainsKey("SQL"));
        Assert.Equal("Dev", job.Options.JobTitle);
    }

    [Fact]
    public void EvaluateEligibility_duplicate_resume_key_zeroes_second_option()
    {
        using var fixture = new EligibilityFixture(null,
            EligibilityFixture.Option("skill", 100, "rust", "Rust"),
            EligibilityFixture.Option("skill", 100, "cargo", "Rust"));

        var job = EligibilityFixture.MakeJob("rust and cargo");

        Assert.False(fixture.Helper.EvaluateEligibility(job, out var rejected));
        Assert.False(rejected);
        Assert.Equal(100L, job.Score);
        Assert.Contains("**(+100) Rust**", job.Log);
        Assert.Contains("**(+0) Rust**", job.Log);
    }

    [Fact]
    public void EvaluateEligibility_second_distinct_key_in_category_is_halved()
    {
        using var fixture = new EligibilityFixture(null,
            EligibilityFixture.Option("field", 100, "backend", "Backend"),
            EligibilityFixture.Option("skill", 120, "python", "Python"),
            EligibilityFixture.Option("skill", 100, "rust", "Rust"));

        var job = EligibilityFixture.MakeJob("backend python rust");

        Assert.True(fixture.Helper.EvaluateEligibility(job, out var rejected));
        Assert.False(rejected);
        Assert.Equal(270L, job.Score);
        Assert.Contains("**(+120) Python**", job.Log);
        Assert.Contains("**(+50) Rust**", job.Log);
    }

    [Fact]
    public void EvaluateEligibility_below_threshold_fails_with_computed_score()
    {
        using var fixture = new EligibilityFixture(null,
            EligibilityFixture.Option("field", 60, "backend", "Backend"));

        var job = EligibilityFixture.MakeJob("backend");

        Assert.False(fixture.Helper.EvaluateEligibility(job, out var rejected));
        Assert.False(rejected);
        Assert.Equal(60L, job.Score);
        Assert.Contains("*Field:*", job.Log);
    }
}
