using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class AgencyWaitingTests
{
    private static readonly JsonSerializerOptions wire = new(JsonSerializerDefaults.Web);
    private static SettingsController Controller(CheckpointDatabase db)
    {
        return new SettingsController(db.Analyzer, db.Database);
    }

    private static string StoredSettings(CheckpointDatabase db)
    {
        return db.Database.ExecuteScalar<string>("SELECT Settings FROM Agency WHERE AgencyID = 1")!;
    }

    [Fact]
    public void AgencyWaiting_saves_the_override_and_pacing_uses_it()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.AgencyWaiting(
            new AgencyWaitingContext { Agency = "CheckpointAgency", Waiting = 9000 }));

        Assert.Contains("\"waiting\":9000", StoredSettings(db));
        Assert.Contains("\"methods\"", StoredSettings(db));

        db.Analyzer.ReloadSettings();
        Assert.Equal(9000, db.AgencyById.Pacing);
        Assert.Equal(9000, db.AgencyById.PacingOverride);
    }

    [Fact]
    public void AgencyWaiting_clearing_the_override_removes_the_key_and_falls_back()
    {
        using var db = new CheckpointDatabase(waiting: 9000);
        var controller = Controller(db);

        Assert.Equal(9000, db.AgencyById.Pacing);

        Assert.IsType<OkResult>(controller.AgencyWaiting(
            new AgencyWaitingContext { Agency = "CheckpointAgency" }));

        Assert.DoesNotContain("waiting", StoredSettings(db));

        db.Analyzer.ReloadSettings();
        Assert.Null(db.AgencyById.PacingOverride);
        Assert.Equal(2500, db.AgencyById.Pacing);
    }

    [Fact]
    public void AgencyWaiting_guards_a_missing_or_unknown_agency()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<BadRequestResult>(controller.AgencyWaiting(null));
        Assert.IsType<BadRequestResult>(controller.AgencyWaiting(new AgencyWaitingContext()));
        Assert.IsType<NotFoundResult>(controller.AgencyWaiting(
            new AgencyWaitingContext { Agency = "Nope", Waiting = 1000 }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(600_001)]
    public void AgencyWaiting_rejects_out_of_range_values(int waiting)
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        var action = controller.AgencyWaiting(
            new AgencyWaitingContext { Agency = "CheckpointAgency", Waiting = waiting });

        var bad = Assert.IsType<BadRequestObjectResult>(action);
        var body = JsonSerializer.SerializeToElement(bad.Value, wire);
        Assert.Equal("waiting-out-of-range", body.GetProperty("error").GetString());
    }
}
