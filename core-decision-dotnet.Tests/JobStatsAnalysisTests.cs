namespace Photon.JobSeeker.Tests;

public class JobStatsAnalysisTests
{
    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

    private static void Seed(GoldenDatabase db, string code, JobState state,
        DateTime regTime, DateTime modifiedOn, DateTime? publishedAt = null)
    {
        db.Database.Job.InsertFromSearch(GoldenDatabase.AgencyId, "NL", $"https://example.com/jobs/{code}", code);
        db.ExecuteRaw($@"
UPDATE Job SET State = '{state}', RegTime = @reg, ModifiedOn = @mod, PublishedAt = @pub
WHERE Code = @code",
            ("reg", Stamp(regTime)),
            ("mod", Stamp(modifiedOn)),
            ("pub", publishedAt == null ? DBNull.Value : (object)Stamp(publishedAt.Value)),
            ("code", code));
    }

    private static void SeedScores(GoldenDatabase db, string code, long? score, int? aiScore)
    {
        db.ExecuteRaw("UPDATE Job SET Score = @score, AiScore = @ai WHERE Code = @code",
            ("score", score == null ? DBNull.Value : (object)score.Value),
            ("ai", aiScore == null ? DBNull.Value : (object)aiScore.Value),
            ("code", code));
    }

    private static void SeedAiFields(GoldenDatabase db, string code,
        string? workModel, string? relocation, string? seniority, string? contract)
    {
        db.ExecuteRaw(@"
UPDATE Job SET AiWorkModel = @wm, AiRelocation = @rl, AiSeniority = @sn, AiContract = @ct
WHERE Code = @code",
            ("wm", workModel == null ? DBNull.Value : (object)workModel),
            ("rl", relocation == null ? DBNull.Value : (object)relocation),
            ("sn", seniority == null ? DBNull.Value : (object)seniority),
            ("ct", contract == null ? DBNull.Value : (object)contract),
            ("code", code));
    }

    private static void SeedVerdict(GoldenDatabase db, string code, string verdict)
    {
        db.ExecuteRaw("UPDATE Job SET AiVerdict = @v WHERE Code = @code", ("v", verdict), ("code", code));
    }

    private static void SeedExperience(GoldenDatabase db, string code, int? years,
        long? score = null, int? aiScore = null)
    {
        db.ExecuteRaw("UPDATE Job SET AiExperienceYears = @years, Score = @score, AiScore = @ai WHERE Code = @code",
            ("years", years == null ? DBNull.Value : (object)years.Value),
            ("score", score == null ? DBNull.Value : (object)score.Value),
            ("ai", aiScore == null ? DBNull.Value : (object)aiScore.Value),
            ("code", code));
    }

    [Fact]
    public void StatsScoreHistograms_bins_normalized_scores_and_ai_scores()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;

        Seed(db, "j1", JobState.Saved, now, now);
        Seed(db, "j2", JobState.Saved, now, now);
        Seed(db, "j3", JobState.Saved, now, now);
        Seed(db, "j4", JobState.Saved, now, now);
        Seed(db, "j5", JobState.Saved, now, now);
        Seed(db, "j6", JobState.Saved, now, now);
        SeedScores(db, "j1", 150, 59);
        SeedScores(db, "j2", 300, 60);
        SeedScores(db, "j3", null, 100);
        SeedScores(db, "j4", 0, null);
        SeedScores(db, "j5", null, null);
        SeedScores(db, "j6", 999, null);

        var histograms = db.Database.Job.StatsScoreHistograms([], []);

        Assert.Equal(60, histograms.AiPassmark);
        Assert.Equal(4, histograms.RegexJobs);
        Assert.Equal(3, histograms.AiJobs);

        Assert.Equal(10, histograms.Labels.Count);
        Assert.Equal("0-9", histograms.Labels[0]);
        Assert.Equal("50-59", histograms.Labels[5]);
        Assert.Equal("90-100", histograms.Labels[9]);

        Assert.Equal(1, histograms.RegexBins[0]);
        Assert.Equal(1, histograms.RegexBins[5]);
        Assert.Equal(2, histograms.RegexBins[9]);
        Assert.Equal(4, histograms.RegexBins.Sum());

        Assert.Equal(1, histograms.AiBins[5]);
        Assert.Equal(1, histograms.AiBins[6]);
        Assert.Equal(1, histograms.AiBins[9]);
        Assert.Equal(3, histograms.AiBins.Sum());
    }

    [Fact]
    public void StatsAiDonuts_count_gate_passed_market_fields_only()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;

        Seed(db, "j1", JobState.Attention, now, now);
        Seed(db, "j2", JobState.Applied, now, now);
        Seed(db, "j3", JobState.Rejected, now, now);
        Seed(db, "j4", JobState.NotApprovedRegex, now, now);
        Seed(db, "j5", JobState.Saved, now, now);
        SeedAiFields(db, "j1", "Remote", "Yes", "Senior", "Permanent");
        SeedAiFields(db, "j2", "Remote", null, "Senior", "B2B");
        SeedAiFields(db, "j3", "Hybrid", "No", "Mid", "Permanent");
        SeedAiFields(db, "j4", "Remote", "Yes", "Senior", "Permanent");
        SeedAiFields(db, "j5", "Remote", "Yes", "Senior", "Permanent");

        var donuts = db.Database.Job.StatsAiDonuts([], []);

        Assert.Equal(["workModel", "relocation", "seniority", "contract"], donuts.Select(donut => donut.Key));

        var workModel = donuts[0].Slices;
        Assert.Equal("Hybrid", workModel[0].Label);
        Assert.Equal(1, workModel[0].Jobs);
        Assert.Equal("Remote", workModel[1].Label);
        Assert.Equal(2, workModel[1].Jobs);

        Assert.Equal([("Yes", 1L), ("No", 1L)],
            donuts[1].Slices.Select(slice => (slice.Label, slice.Jobs)));
        Assert.Equal([("Mid", 1L), ("Senior", 2L)],
            donuts[2].Slices.Select(slice => (slice.Label, slice.Jobs)));
        Assert.Equal([("Permanent", 2L), ("B2B", 1L)],
            donuts[3].Slices.Select(slice => (slice.Label, slice.Jobs)));
    }

    [Fact]
    public void StatsAiVerdict_counts_verdicts_across_all_states()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;

        Seed(db, "j1", JobState.Attention, now, now);
        Seed(db, "j2", JobState.NotApprovedAI, now, now);
        Seed(db, "j3", JobState.Rejected, now, now);
        Seed(db, "j4", JobState.Saved, now, now);
        Seed(db, "j5", JobState.Attention, now, now);
        SeedVerdict(db, "j1", "StrongMatch");
        SeedVerdict(db, "j2", "NoMatch");
        SeedVerdict(db, "j3", "Match");
        SeedVerdict(db, "j4", "Error");

        var donut = db.Database.Job.StatsAiVerdict([], []);

        Assert.Equal([("StrongMatch", 1L), ("Match", 1L), ("NoMatch", 1L), ("Error", 1L)],
            donut.Slices.Select(slice => (slice.Label, slice.Jobs)));
    }

    [Fact]
    public void StatsCompetitiveness_buckets_by_experience_with_pass_rate_and_score()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;

        Seed(db, "c1", JobState.NotApprovedAI, now, now, now);
        Seed(db, "c2", JobState.Attention, now, now, now);
        Seed(db, "c3", JobState.Rejected, now, now, now);
        Seed(db, "c4", JobState.Attention, now, now, now);
        Seed(db, "c5", JobState.Attention, now, now, now);
        Seed(db, "c6", JobState.Saved, now, now, now);
        SeedExperience(db, "c1", 1);
        SeedExperience(db, "c2", 2, score: 300, aiScore: 70);
        SeedExperience(db, "c3", 20, score: 150);
        SeedExperience(db, "c4", null);
        SeedExperience(db, "c5", -1);
        SeedExperience(db, "c6", 4);

        var buckets = db.Database.Job.StatsCompetitiveness([], []);

        Assert.Equal(["0-2", "3-5", "6-9", "10-14", "15+"], buckets.Select(bucket => bucket.Bucket));

        var young = buckets[0];
        Assert.Equal(2, young.Evaluated);
        Assert.Equal(1, young.Passed);
        Assert.Equal(50, young.PassRate);
        Assert.NotNull(young.AvgEffectiveScore);
        Assert.InRange(young.AvgEffectiveScore!.Value, 34.1, 34.3);

        var mid = buckets[1];
        Assert.Equal(0, mid.Evaluated);
        Assert.Equal(0, mid.Passed);
        Assert.Null(mid.AvgEffectiveScore);

        Assert.All(buckets.Skip(2).Take(2), bucket =>
        {
            Assert.Equal(0, bucket.Evaluated);
            Assert.Null(bucket.AvgEffectiveScore);
        });

        var old = buckets[4];
        Assert.Equal(1, old.Evaluated);
        Assert.Equal(1, old.Passed);
        Assert.Equal(100, old.PassRate);
        Assert.Equal(42.5, old.AvgEffectiveScore);
    }

    [Fact]
    public void StatsAttentionAging_buckets_backlog_by_age()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;

        Seed(db, "a1", JobState.Attention, now, now, now.AddDays(-1));
        Seed(db, "a2", JobState.Attention, now, now, now.AddDays(-3));
        Seed(db, "a3", JobState.Attention, now, now, now.AddDays(-9));
        Seed(db, "a4", JobState.Attention, now, now, now.AddDays(-20));
        Seed(db, "a5", JobState.Attention, now.AddDays(-5), now);
        Seed(db, "x1", JobState.Applied, now, now, now.AddDays(-20));

        var aging = db.Database.Job.StatsAttentionAging([], []);

        Assert.Equal(5, aging.Total);
        Assert.Equal([("0-2", 1L), ("2-7", 2L), ("7-14", 1L), (">14", 1L)],
            aging.Buckets.Select(bucket => (bucket.Label, bucket.Jobs)));
    }

    [Fact]
    public void StatsDispositionTimes_buckets_days_and_computes_median_p90()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;
        var published = now.AddDays(-30);

        Seed(db, "d1", JobState.Applied, published, published.AddHours(12), published);
        Seed(db, "d2", JobState.Rejected, published, published.AddDays(2.5), published);
        Seed(db, "d3", JobState.Applied, published, published.AddDays(5.5), published);
        Seed(db, "d4", JobState.Rejected, published, published.AddDays(10.5), published);
        Seed(db, "d5", JobState.Applied, published, published.AddDays(20.5), published);
        Seed(db, "x1", JobState.Attention, published, published.AddDays(20.5), published);

        var disposition = db.Database.Job.StatsDispositionTimes([], []);

        Assert.Equal(5, disposition.Jobs);
        Assert.Equal(5.5, disposition.MedianDays);
        Assert.Equal(20.5, disposition.P90Days);
        Assert.Equal([("0-1", 1L), ("2-3", 1L), ("4-7", 1L), ("8-14", 1L), ("15+", 1L)],
            disposition.Buckets.Select(bucket => (bucket.Label, bucket.Jobs)));
    }

    [Fact]
    public void StatsDispositionTimes_on_empty_database_returns_zeroed_histogram()
    {
        using var db = new GoldenDatabase();

        var disposition = db.Database.Job.StatsDispositionTimes([], []);

        Assert.Equal(0, disposition.Jobs);
        Assert.Null(disposition.MedianDays);
        Assert.Null(disposition.P90Days);
        Assert.Equal(5, disposition.Buckets.Count);
        Assert.All(disposition.Buckets, bucket => Assert.Equal(0, bucket.Jobs));
    }
}
