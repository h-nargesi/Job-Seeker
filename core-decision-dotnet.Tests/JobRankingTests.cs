namespace Photon.JobSeeker.Tests;

public class JobRankingTests
{
    [Theory]
    [InlineData(0, 0.85)]
    [InlineData(2, 0.85)]
    [InlineData(3, 0.925)]
    [InlineData(4, 1.0)]
    [InlineData(7, 1.0)]
    [InlineData(10, 1.0)]
    [InlineData(12, 0.875)]
    [InlineData(14, 0.75)]
    [InlineData(21, 0.5)]
    [InlineData(28, 0.25)]
    [InlineData(35, 0.15)]
    [InlineData(-5, 0.85)]
    public void Weight_matches_trapezoid_breakpoints(double ageDays, double expected)
    {
        Assert.Equal(expected, JobRanking.Weight(ageDays), 10);
    }

    [Fact]
    public void Weight_hits_named_constants_at_key_ages()
    {
        Assert.Equal(JobRanking.FreshPenalty, JobRanking.Weight(1), 10);
        Assert.Equal(1.0, JobRanking.Weight(JobRanking.SweetStart), 10);
        Assert.Equal(1.0, JobRanking.Weight(JobRanking.SweetEnd), 10);
        Assert.Equal(JobRanking.TwoWeekWeight, JobRanking.Weight(14), 10);
        Assert.Equal(JobRanking.OldWeight, JobRanking.Weight(28), 10);
        Assert.Equal(JobRanking.Floor, JobRanking.Weight(60), 10);
    }

    [Fact]
    public void Weight_stays_within_bounds()
    {
        for (var age = 0.0; age <= 60.0; age += 0.05)
        {
            var weight = JobRanking.Weight(age);
            Assert.True(weight >= JobRanking.Floor - 1e-9, $"below floor at age {age}");
            Assert.True(weight <= 1.0 + 1e-9, $"above ceiling at age {age}");
        }
    }

    [Fact]
    public void Weight_rises_to_sweet_spot_then_decays()
    {
        var previous = JobRanking.Weight(0);
        for (var age = 0.05; age <= JobRanking.SweetEnd; age += 0.05)
        {
            var weight = JobRanking.Weight(age);
            Assert.True(weight >= previous - 1e-9, $"dipped while rising at age {age}");
            previous = weight;
        }

        for (var age = JobRanking.SweetEnd + 0.05; age <= 60.0; age += 0.05)
        {
            var weight = JobRanking.Weight(age);
            Assert.True(weight <= previous + 1e-9, $"rose while decaying at age {age}");
            previous = weight;
        }
    }

    [Fact]
    public void Effective_multiplies_score_by_weight()
    {
        Assert.Equal(200, JobRanking.Effective(200, 7), 10);
        Assert.Equal(85, JobRanking.Effective(100, 0), 10);
        Assert.Equal(180, JobRanking.Effective(240, 14), 10);
        Assert.Equal(30, JobRanking.Effective(200, 35), 10);
        Assert.Equal(0, JobRanking.Effective(0, 7), 10);
    }
}
