using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class DecisionControllerTests
{
    private static readonly JsonSerializerOptions wire = new(JsonSerializerDefaults.Web);

    private static DecisionController Controller(CheckpointDatabase db)
    {
        return new DecisionController(db.Analyzer, db.Database, new TrendsCheckpoint(db.Analyzer, db.Database, new Result()));
    }

    private static JsonElement OkBody(IActionResult action)
    {
        var ok = Assert.IsType<OkObjectResult>(action);
        return JsonSerializer.SerializeToElement(ok.Value, wire);
    }

    private static JsonElement BadBody(IActionResult action)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(action);
        return JsonSerializer.SerializeToElement(bad.Value, wire);
    }

    [Fact]
    public void Take_without_a_context_is_rejected()
    {
        using var db = new CheckpointDatabase();

        var body = BadBody(Controller(db).Take(null));

        Assert.Equal("missing-context", body.GetProperty("error").GetString());
    }

    [Fact]
    public void Take_with_an_unknown_agency_is_a_bad_job_request()
    {
        using var db = new CheckpointDatabase();
        var context = new PageContext { Agency = "Nope", Url = "https://cp.example.com/", Content = "<html/>" };

        var body = BadBody(Controller(db).Take(context));

        Assert.Equal("bad-job-request", body.GetProperty("error").GetString());
    }

    [Fact]
    public void Take_with_an_empty_url_or_content_is_a_bad_job_request()
    {
        using var db = new CheckpointDatabase();
        var context = new PageContext { Agency = "CheckpointAgency", Content = "<html/>" };

        var body = BadBody(Controller(db).Take(context));

        Assert.Equal("bad-job-request", body.GetProperty("error").GetString());
    }

    [Fact]
    public void Take_returns_trend_commands_and_the_default_close_timeout()
    {
        using var db = new CheckpointDatabase();
        db.Agency.PageState = TrendState.Other;
        db.Agency.PageCommands = [];
        var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/jobs", Content = "<html></html>" };

        var body = OkBody(Controller(db).Take(context));

        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("trend").ValueKind);
        Assert.True(body.GetProperty("trend").GetInt64() > 0);
        Assert.Equal(90_000, body.GetProperty("close_timeout_ms").GetInt64());

        var commands = body.GetProperty("commands").EnumerateArray().ToList();
        Assert.Equal(2, commands.Count);
        Assert.Equal("open", commands[0].GetProperty("action").GetString());
        Assert.Equal("close", commands[1].GetProperty("action").GetString());
    }

    [Fact]
    public void Take_in_auth_state_extends_the_close_timeout_to_ten_minutes()
    {
        using var db = new CheckpointDatabase();
        db.Agency.PageState = TrendState.Auth;
        db.Agency.PageCommands = [];
        var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/login", Content = "<html></html>" };

        var body = OkBody(Controller(db).Take(context));

        Assert.Equal(600_000, body.GetProperty("close_timeout_ms").GetInt64());
    }

    [Fact]
    public void Heartbeat_touches_a_live_trend_and_answers_null_for_unknown_ids()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        var trend_id = db.Database.Trend.Get(1, TrendType.Search)!.TrendID;

        var controller = Controller(db);

        var touched = OkBody(controller.Heartbeat(new HeartbeatContext(trend_id)));
        Assert.Equal(trend_id, touched.GetProperty("trend").GetInt64());

        var missed = OkBody(controller.Heartbeat(new HeartbeatContext(987_654)));
        Assert.Equal(JsonValueKind.Null, missed.GetProperty("trend").ValueKind);

        var without_context = OkBody(controller.Heartbeat(null));
        Assert.Equal(JsonValueKind.Null, without_context.GetProperty("trend").ValueKind);
    }

    [Fact]
    public void Reset_wipes_trend_rows_and_reloads_the_agency_cache()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking, Reserved = true });

        Assert.IsType<OkResult>(Controller(db).Reset());

        Assert.Equal(0L, db.CountTrends());
        Assert.Contains("CheckpointAgency", db.Analyzer.Agencies.Keys);
    }

    [Fact]
    public void Scopes_lists_active_agencies_with_their_domains()
    {
        using var db = new CheckpointDatabase();

        var body = OkBody(Controller(db).Scopes());

        var scope = Assert.Single(body.EnumerateArray());
        Assert.Equal("CheckpointAgency", scope.GetProperty("name").GetString());
        Assert.Equal("cp\\.example\\.com$", scope.GetProperty("domain").GetString());
        Assert.Equal(0, scope.GetProperty("waiting").GetInt64());
    }

    [Fact]
    public void Orders_runs_the_checkpoint_and_opens_the_idle_search_page()
    {
        using var db = new CheckpointDatabase();

        var body = OkBody(Controller(db).Orders());

        var commands = body.GetProperty("commands").EnumerateArray().ToList();
        Assert.Equal(2, commands.Count);
        Assert.Equal("open", commands[0].GetProperty("action").GetString());
        Assert.Equal("https://cp.example.com/jobs", commands[0].GetProperty("params").GetProperty("url").GetString());
        Assert.Equal("close", commands[1].GetProperty("action").GetString());
    }

    [Fact]
    public void Running_guards_a_missing_or_unknown_agency()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<BadRequestResult>(controller.Running(new RunningMethodContext()));
        Assert.IsType<NotFoundResult>(controller.Running(new RunningMethodContext { Agency = "Nope" }));
    }

    [Fact]
    public void Running_sets_the_method_clears_search_trends_and_stops_cleanly()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Running(new RunningMethodContext { Agency = "CheckpointAgency", Running = 2 }));

        Assert.Equal(2, db.Agency.CurrentMethodIndex);
        Assert.True(db.Agency.IsActiveSeeking);
        Assert.Null(db.Database.Trend.Get(1, TrendType.Search));

        Assert.IsType<OkResult>(controller.Running(new RunningMethodContext { Agency = "CheckpointAgency" }));

        Assert.False(db.Agency.IsActiveSeeking);
    }
}
