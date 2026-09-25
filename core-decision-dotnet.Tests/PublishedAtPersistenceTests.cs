namespace Photon.JobSeeker.Tests;

public class PublishedAtPersistenceTests
{
    private static Job NewJob(string code, DateTime? publishedAt = null)
    {
        return new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = code,
            State = JobState.Saved,
            Url = $"https://example.com/jobs/{code}",
            Title = "Dev",
            PublishedAt = publishedAt,
        };
    }

    [Fact]
    public void InsertJob_Persists_Published_At()
    {
        using var db = new GoldenDatabase();
        var published = new DateTime(2026, 3, 10, 18, 43, 26);

        db.Database.Job.InsertJob(NewJob("p1", published));

        var loaded = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "p1");
        Assert.NotNull(loaded);
        Assert.Equal(published, loaded!.PublishedAt);
    }

    [Fact]
    public void InsertJob_Without_Published_At_Leaves_Column_Null()
    {
        using var db = new GoldenDatabase();

        db.Database.Job.InsertJob(NewJob("p2"));

        Assert.Equal(DBNull.Value, db.Scalar("SELECT PublishedAt FROM Job"));
        Assert.Null(db.Database.Job.Fetch(GoldenDatabase.AgencyId, "p2")!.PublishedAt);
    }

    [Fact]
    public void UpdateScrapedJob_Writes_Published_At_When_Set()
    {
        using var db = new GoldenDatabase();
        db.Database.Job.InsertJob(NewJob("p3"));
        var updated = new DateTime(2026, 9, 1, 12, 0, 0);

        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "p3")!;
        job.PublishedAt = updated;
        db.Database.Job.UpdateScrapedJob(job, codeChanged: false, linkFound: false, includeState: false);

        Assert.Equal(updated, db.Database.Job.Fetch(GoldenDatabase.AgencyId, "p3")!.PublishedAt);
    }

    [Fact]
    public void UpdateScrapedJob_Omits_Published_At_When_Null_And_Value_Survives()
    {
        using var db = new GoldenDatabase();
        var published = new DateTime(2026, 9, 1, 12, 0, 0);
        db.Database.Job.InsertJob(NewJob("p4", published));

        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "p4")!;
        job.PublishedAt = null;
        db.Database.Job.UpdateScrapedJob(job, codeChanged: false, linkFound: false, includeState: false);

        Assert.Equal(published, db.Database.Job.Fetch(GoldenDatabase.AgencyId, "p4")!.PublishedAt);
    }

    [Fact]
    public void Fetch_Ranking_Uses_Published_At_Age_Over_Reg_Time()
    {
        using var db = new GoldenDatabase();
        var reg = new DateTime(2026, 9, 25, 0, 0, 0);
        db.SaveSearchJob("fresh", "https://example.com/jobs/fresh");
        db.SaveSearchJob("sweet", "https://example.com/jobs/sweet");
        db.ExecuteRaw(
            @"UPDATE Job SET RegTime = $reg, Score = 150, State = 'Attention', PublishedAt = $pub WHERE Code = 'sweet'",
            ("$reg", reg), ("$pub", reg.AddDays(-6)));
        db.ExecuteRaw(
            @"UPDATE Job SET RegTime = $reg, Score = 150, State = 'Attention' WHERE Code = 'fresh'",
            ("$reg", reg));

        var list = db.Database.Job.Fetch([], []);

        Assert.Equal("sweet", list.First().Job.Code);
        Assert.Equal("fresh", list.Last().Job.Code);
        Assert.Equal(reg.AddDays(-6), list.Single(x => x.Job.Code == "sweet").Job.PublishedAt);
        Assert.Null(list.Single(x => x.Job.Code == "fresh").Job.PublishedAt);
    }

    [Fact]
    public void Fetch_Ranking_Tiebreak_Orders_By_Published_At_When_Scores_Tie()
    {
        using var db = new GoldenDatabase();
        var reg = new DateTime(2026, 9, 25, 0, 0, 0);
        db.SaveSearchJob("t1", "https://example.com/jobs/t1");
        db.SaveSearchJob("t2", "https://example.com/jobs/t2");
        db.ExecuteRaw(
            @"UPDATE Job SET RegTime = $reg, Score = 150, State = 'Attention', PublishedAt = $pub WHERE Code = 't1'",
            ("$reg", reg), ("$pub", reg.AddDays(-5)));
        db.ExecuteRaw(
            @"UPDATE Job SET RegTime = $reg, Score = 150, State = 'Attention', PublishedAt = $pub WHERE Code = 't2'",
            ("$reg", reg), ("$pub", reg.AddDays(-8)));

        var list = db.Database.Job.Fetch([], []);

        Assert.Equal("t1", list.First().Job.Code);
        Assert.Equal("t2", list.Last().Job.Code);
    }
}
