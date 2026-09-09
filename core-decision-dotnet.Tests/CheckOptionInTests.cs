namespace Photon.JobSeeker.Tests;

public class CheckOptionInTests
{
    [Fact]
    public void Repeated_identical_matches_are_deduped_to_one_line()
    {
        var option = EligibilityFixture.Option("skill", 7, "sql", "Sql");
        var job = EligibilityFixture.MakeJob("sql and sql and sql");

        var score = JobEligibilityHelper.CheckOptionIn(job, option, out var matched);

        Assert.Equal(7, score);
        Assert.Equal("sql", matched);
    }

    [Fact]
    public void Dedupe_is_case_insensitive_and_keeps_first_casing()
    {
        var option = EligibilityFixture.Option("skill", 7, "sql", "Sql");
        var job = EligibilityFixture.MakeJob("Sql SQL sQl");

        var score = JobEligibilityHelper.CheckOptionIn(job, option, out var matched);

        Assert.Equal(7, score);
        Assert.Equal("Sql", matched);
    }

    [Fact]
    public void Whitespace_only_matches_are_skipped()
    {
        var option = EligibilityFixture.Option("skill", 7, @"\s+", "Spaces");
        var job = EligibilityFixture.MakeJob("one   two    three");

        var score = JobEligibilityHelper.CheckOptionIn(job, option, out var matched);

        Assert.Equal(0, score);
        Assert.Equal(string.Empty, matched);
    }

    [Fact]
    public void Salary_category_first_positive_match_wins()
    {
        var option = EligibilityFixture.SalaryOption(2, 1, 0, @"(\d[\d,.]*k?)");
        var job = EligibilityFixture.MakeJob("5000 then 120k");

        var score = JobEligibilityHelper.CheckOptionIn(job, option, out var matched);

        Assert.Equal(10, score);
        Assert.Equal("5000\n120k", matched);
    }

    [Fact]
    public void Non_salary_category_returns_flat_option_score()
    {
        var option = EligibilityFixture.Option("skill", 9, "sql", "Sql");
        var job = EligibilityFixture.MakeJob("sql sql sql");

        var score = JobEligibilityHelper.CheckOptionIn(job, option, out var matched);

        Assert.Equal(9, score);
        Assert.Equal("sql", matched);
    }

    [Fact]
    public void Null_content_matches_nothing()
    {
        var option = EligibilityFixture.Option("skill", 9, "sql", "Sql");
        var job = EligibilityFixture.MakeJob(null);

        var score = JobEligibilityHelper.CheckOptionIn(job, option, out var matched);

        Assert.Equal(0, score);
        Assert.Equal(string.Empty, matched);
    }
}
