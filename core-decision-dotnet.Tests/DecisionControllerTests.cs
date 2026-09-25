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
        Assert.Equal(3, commands.Count);
        Assert.Equal("wait", commands[0].GetProperty("action").GetString());
        Assert.InRange(commands[0].GetProperty("params").GetProperty("miliseconds").GetInt64(), 1875, 3125);
        Assert.Equal("open", commands[1].GetProperty("action").GetString());
        Assert.Equal("close", commands[2].GetProperty("action").GetString());
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
    public void Take_with_a_challenge_holds_the_trend_and_extends_the_close_timeout()
    {
        using var db = new CheckpointDatabase();
        var context = new PageContext
        {
            Agency = "CheckpointAgency",
            Url = "https://cp.example.com/jobs",
            Content = "<html></html>",
            Challenge = true,
        };

        var body = OkBody(Controller(db).Take(context));

        Assert.True(body.GetProperty("trend").GetInt64() > 0);
        Assert.Equal(86_400_000, body.GetProperty("close_timeout_ms").GetInt64());
        Assert.Empty(body.GetProperty("commands").EnumerateArray());

        var row = db.Database.Trend.Get(1, TrendType.Search);
        Assert.NotNull(row);
        Assert.True(row!.Challenge);
    }

    [Fact]
    public void Take_with_a_challenge_on_an_unknown_agency_is_a_bad_job_request()
    {
        using var db = new CheckpointDatabase();
        var context = new PageContext { Agency = "Nope", Url = "https://cp.example.com/", Challenge = true };

        var body = BadBody(Controller(db).Take(context));

        Assert.Equal("bad-job-request", body.GetProperty("error").GetString());
    }

    [Fact]
    public void PageContext_binds_challenge_kind_from_json_and_the_hold_response_is_unchanged()
    {
        using var db = new CheckpointDatabase();
        var context = JsonSerializer.Deserialize<PageContext>(@"{
            ""agency"": ""CheckpointAgency"",
            ""url"": ""https://cp.example.com/jobs"",
            ""content"": ""<html></html>"",
            ""challenge"": true,
            ""challenge_kind"": ""http-403""
        }", wire);

        Assert.NotNull(context);
        Assert.True(context!.Challenge);
        Assert.Equal("http-403", context.ChallengeKind);
        Assert.Contains("http-403", context.ToString());

        var body = OkBody(Controller(db).Take(context));

        Assert.True(body.GetProperty("trend").GetInt64() > 0);
        Assert.Equal(86_400_000, body.GetProperty("close_timeout_ms").GetInt64());
        Assert.Empty(body.GetProperty("commands").EnumerateArray());

        var row = db.Database.Trend.Get(1, TrendType.Search);
        Assert.NotNull(row);
        Assert.True(row!.Challenge);
    }

    [Fact]
    public void Take_inserts_a_jittered_pacing_wait_from_the_settings_override()
    {
        using var db = new CheckpointDatabase(waiting: 9000);
        db.Agency.PageState = TrendState.Seeking;
        db.Agency.PageCommands = [Command.Click("#apply")];
        var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/jobs", Content = "<html></html>" };

        var commands = OkBody(Controller(db).Take(context)).GetProperty("commands").EnumerateArray().ToList();

        Assert.Equal(2, commands.Count);
        Assert.Equal("wait", commands[0].GetProperty("action").GetString());
        Assert.InRange(commands[0].GetProperty("params").GetProperty("miliseconds").GetInt64(), 6750, 11250);
        Assert.Equal("click", commands[1].GetProperty("action").GetString());
        Assert.Equal("#apply", commands[1].GetProperty("object").GetString());
    }

    [Fact]
    public void Take_inserts_the_pacing_wait_immediately_before_the_first_trigger_command()
    {
        using var db = new CheckpointDatabase();
        db.Agency.PageState = TrendState.Seeking;
        db.Agency.PageCommands =
        [
            Command.Fill("#user", "ryan"),
            Command.Click("#submit"),
            Command.Go("https://cp.example.com/next"),
        ];
        var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/login", Content = "<html></html>" };

        var commands = OkBody(Controller(db).Take(context)).GetProperty("commands").EnumerateArray().ToList();

        Assert.Equal(4, commands.Count);
        Assert.Equal("fill", commands[0].GetProperty("action").GetString());
        Assert.Equal("wait", commands[1].GetProperty("action").GetString());
        Assert.InRange(commands[1].GetProperty("params").GetProperty("miliseconds").GetInt64(), 1875, 3125);
        Assert.Equal("click", commands[2].GetProperty("action").GetString());
        Assert.Equal("go", commands[3].GetProperty("action").GetString());
    }

    [Fact]
    public void Take_without_a_trigger_command_inserts_no_pacing_wait()
    {
        var seeds = new[]
        {
            new[] { Command.Reload() },
            new[] { Command.Close() },
            new[] { Command.Wait(3000), Command.Recheck() },
        };

        foreach (var seed in seeds)
        {
            using var db = new CheckpointDatabase();
            db.Agency.PageState = TrendState.Seeking;
            db.Agency.PageCommands = seed;
            var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/jobs", Content = "<html></html>" };

            var commands = OkBody(Controller(db).Take(context)).GetProperty("commands").EnumerateArray().ToList();

            Assert.Equal(seed.Select(c => c.Action), commands.Select(c => c.GetProperty("action").GetString()));
        }
    }

    [Fact]
    public void Take_in_auth_state_appends_only_the_close_command_without_pacing()
    {
        using var db = new CheckpointDatabase();
        db.Agency.PageState = TrendState.Auth;
        db.Agency.PageCommands = [];
        var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/login", Content = "<html></html>" };

        var commands = OkBody(Controller(db).Take(context)).GetProperty("commands").EnumerateArray().ToList();

        Assert.Equal("close", Assert.Single(commands).GetProperty("action").GetString());
    }

    [Fact]
    public void Take_with_zero_pacing_is_disabled()
    {
        using var db = new CheckpointDatabase(waiting: 0);
        db.Agency.PageState = TrendState.Seeking;
        db.Agency.PageCommands = [Command.Click("#apply")];
        var context = new PageContext { Agency = "CheckpointAgency", Url = "https://cp.example.com/jobs", Content = "<html></html>" };

        var commands = OkBody(Controller(db).Take(context)).GetProperty("commands").EnumerateArray().ToList();

        Assert.Equal("click", Assert.Single(commands).GetProperty("action").GetString());
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
        Assert.Equal(2500, scope.GetProperty("waiting").GetInt64());
    }

    [Fact]
    public void Scopes_serves_the_hardcoded_waiting_despite_the_settings_override()
    {
        using var db = new CheckpointDatabase(waiting: 9000);

        var body = OkBody(Controller(db).Scopes());

        var scope = Assert.Single(body.EnumerateArray());
        Assert.Equal(2500, scope.GetProperty("waiting").GetInt64());
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
        Assert.IsType<NotFoundResult>(controller.Running(new RunningMethodContext { Agency = "Nope", Running = 0 }));
    }

    [Fact]
    public void Running_guards_a_null_or_out_of_range_index()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<BadRequestResult>(controller.Running(
            new RunningMethodContext { Agency = "CheckpointAgency" }));

        var body = BadBody(controller.Running(
            new RunningMethodContext { Agency = "CheckpointAgency", Running = 3 }));
        Assert.Equal("running-out-of-range", body.GetProperty("error").GetString());
    }

    [Fact]
    public void Running_sets_the_method_clears_search_trends_and_turns_seeking_on()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Running(new RunningMethodContext { Agency = "CheckpointAgency", Running = 2 }));

        Assert.Equal(2, db.Agency.CurrentMethodIndex);
        Assert.True(db.Agency.IsActiveSeeking);
        Assert.Null(db.Database.Trend.Get(1, TrendType.Search));
    }

    [Fact]
    public void Status_guards_a_missing_or_unknown_agency()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<BadRequestResult>(controller.Status(null));
        Assert.IsType<BadRequestResult>(controller.Status(new AgencyStatusContext()));
        Assert.IsType<NotFoundResult>(controller.Status(new AgencyStatusContext { Agency = "Nope" }));
    }

    [Fact]
    public void Status_rejects_seeking_without_any_searching_method()
    {
        using var db = new CheckpointDatabase(no_settings: true);
        var controller = Controller(db);

        var body = BadBody(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Seeking = true }));
        Assert.Equal("no-methods", body.GetProperty("error").GetString());
    }

    [Fact]
    public void Status_toggles_analyzing_and_persists_the_active_column()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = false }));

        Assert.False(db.Agency.IsActiveAnalyzing);
        Assert.Equal(1L, db.Database.ExecuteScalar<long>("SELECT Active FROM Agency WHERE AgencyID = 1"));

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = true }));

        Assert.True(db.Agency.IsActiveAnalyzing);
        Assert.Equal(3L, db.Database.ExecuteScalar<long>("SELECT Active FROM Agency WHERE AgencyID = 1"));
    }

    [Fact]
    public void Status_persists_the_active_column_even_without_settings()
    {
        using var db = new CheckpointDatabase(no_settings: true);
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = false }));

        Assert.False(db.AgencyById.IsActiveAnalyzing);
        Assert.Equal(1L, db.Database.ExecuteScalar<long>("SELECT Active FROM Agency WHERE AgencyID = 1"));
    }

    [Fact]
    public void Status_turning_seeking_back_on_deletes_the_blocked_search_trend_and_keeps_the_index()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Running(
            new RunningMethodContext { Agency = "CheckpointAgency", Running = 2 }));

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Seeking = false }));
        Assert.False(db.Agency.IsActiveSeeking);

        db.Database.Trend.Block(1, TrendType.Search);
        Assert.NotNull(db.Database.Trend.Get(1, TrendType.Search));

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Seeking = true }));

        Assert.True(db.Agency.IsActiveSeeking);
        Assert.Equal(2, db.Agency.CurrentMethodIndex);
        Assert.Null(db.Database.Trend.Get(1, TrendType.Search));
    }

    [Fact]
    public void Status_turning_analyzing_back_on_deletes_the_blocked_job_trend()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = false }));

        db.Database.Trend.Block(1, TrendType.Job);
        Assert.NotNull(db.Database.Trend.Get(1, TrendType.Job));

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = true }));

        Assert.True(db.Agency.IsActiveAnalyzing);
        Assert.Null(db.Database.Trend.Get(1, TrendType.Job));
    }

    [Fact]
    public void Status_turning_analyzing_on_when_already_on_keeps_the_live_trend()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Analyzing });
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = true }));

        Assert.NotNull(db.Database.Trend.Get(1, TrendType.Job));
    }

    [Fact]
    public void Status_turning_both_bits_off_removes_the_agency_from_the_active_cache()
    {
        using var db = new CheckpointDatabase();
        var controller = Controller(db);

        Assert.Contains("CheckpointAgency", db.Analyzer.Agencies.Keys);

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Seeking = false, Analyzing = false }));

        Assert.DoesNotContain("CheckpointAgency", db.Analyzer.Agencies.Keys);
        Assert.Contains(1L, db.Analyzer.AgenciesByID.Keys);
    }

    [Fact]
    public void Inactive_agencies_load_into_the_id_cache_but_stay_out_of_scopes()
    {
        using var db = new CheckpointDatabase(active: 0);

        Assert.DoesNotContain("CheckpointAgency", db.Analyzer.Agencies.Keys);
        Assert.False(db.AgencyById.IsActiveSeeking);
        Assert.False(db.AgencyById.IsActiveAnalyzing);

        var scopes = OkBody(Controller(db).Scopes());
        Assert.Empty(scopes.EnumerateArray());
    }

    [Fact]
    public void An_inactive_agency_can_be_re_enabled_from_the_dashboard()
    {
        using var db = new CheckpointDatabase(active: 0);
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.Status(
            new AgencyStatusContext { Agency = "CheckpointAgency", Analyzing = true }));

        Assert.Contains("CheckpointAgency", db.Analyzer.Agencies.Keys);
        Assert.True(db.AgencyById.IsActiveAnalyzing);
        Assert.Equal(2L, db.Database.ExecuteScalar<long>("SELECT Active FROM Agency WHERE AgencyID = 1"));
    }
}
