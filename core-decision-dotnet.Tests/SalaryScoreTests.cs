using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Tests;

public class SalaryScoreTests
{
    private const string SimplePattern = @"(\d[\d,.]*k?)\s*(year|month)?";

    private static long Evaluate(string input, long score, string pattern = SimplePattern,
                                 int moneyGroup = 1, int periodGroup = 2)
    {
        var option = EligibilityFixture.SalaryOption(score, moneyGroup, periodGroup, pattern);
        return JobEligibilityHelper.EvaluateSalaryScore(Regex.Match(input, pattern), option);
    }

    [Fact]
    public void Monthly_period_divides_by_thousand_linearly()
    {
        Assert.Equal(5, Evaluate("5000 month", 1));
        Assert.Equal(10, Evaluate("5000 month", 2));
    }

    [Fact]
    public void Year_period_divides_by_twelve_and_truncates()
    {
        Assert.Equal(7, Evaluate("90000 year", 1));
        Assert.Equal(14, Evaluate("90000 year", 2));
    }

    [Fact]
    public void K_suffix_multiplies_by_thousand_then_divides_by_twelve()
    {
        Assert.Equal(10, Evaluate("120k", 1));
        Assert.Equal(20, Evaluate("120k", 2));
    }

    [Fact]
    public void No_period_above_range_is_treated_as_yearly()
    {
        Assert.Equal(5, Evaluate("60000", 1));
        Assert.Equal(5, Evaluate("60000/month", 1));
    }

    [Fact]
    public void No_period_at_or_below_range_is_treated_as_monthly()
    {
        Assert.Equal(3, Evaluate("3000", 1));
        Assert.Equal(6, Evaluate("3000", 2));
    }

    [Fact]
    public void Per_year_phrase_is_not_recognized_as_period()
    {
        Assert.Equal(7, Evaluate("90000 per year", 1));
    }

    [Fact]
    public void Production_pattern_month_group_is_used()
    {
        const string pattern = @"\bsalary\b.*?(\d[\d,]*(000|k))(.+?\b(month|year)\b)?";

        Assert.Equal(5, Evaluate("salary 5000/month", 1, pattern, 1, 4));
        Assert.Equal(10, Evaluate("salary 120k a year package", 1, pattern, 1, 4));
    }

    [Fact]
    public void Money_group_far_after_match_start_returns_minimum_score()
    {
        var input = new string('a', 30) + "5000";

        Assert.Equal(1, Evaluate(input, 5, @"[a-z]{30,}(\d+)", 1, 2));
    }

    [Fact]
    public void Unparseable_money_text_returns_minimum_score()
    {
        Assert.Equal(1, Evaluate("12.3.4", 5));
    }

    [Fact]
    public void Null_settings_return_zero()
    {
        var option = EligibilityFixture.Option("salary", 5, @"(\d+)");
        var match = Regex.Match("5000", @"(\d+)");

        Assert.Equal(0, JobEligibilityHelper.EvaluateSalaryScore(match, option));
    }
}
