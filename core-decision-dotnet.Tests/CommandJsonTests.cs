using System.Text.Json;

namespace Photon.JobSeeker.Tests;

public class CommandJsonTests
{
    private static readonly JsonSerializerOptions wire = new(JsonSerializerDefaults.Web);

    private static JsonElement Json(Command command)
    {
        return JsonSerializer.SerializeToElement(command, wire);
    }

    [Fact]
    public void Page_actions_match_the_documented_vocabulary_and_order()
    {
        Assert.Equal("go, open, fill, click, recheck, close, wait, reload",
            string.Join(", ", Enum.GetNames<PageAction>()));
    }

    [Fact]
    public void Go_and_open_carry_the_url_param()
    {
        var go = Json(Command.Go("https://example.com/jobs/1"));
        Assert.Equal("go", go.GetProperty("action").GetString());
        Assert.Equal("https://example.com/jobs/1", go.GetProperty("params").GetProperty("url").GetString());

        var open = Json(Command.Open("https://example.com/jobs/2"));
        Assert.Equal("open", open.GetProperty("action").GetString());
        Assert.Equal("https://example.com/jobs/2", open.GetProperty("params").GetProperty("url").GetString());
    }

    [Fact]
    public void Fill_carries_the_object_selector_and_the_value()
    {
        var fill = Json(Command.Fill("#email", "user@example.com"));

        Assert.Equal("fill", fill.GetProperty("action").GetString());
        Assert.Equal("#email", fill.GetProperty("object").GetString());
        Assert.Equal("user@example.com", fill.GetProperty("params").GetProperty("value").GetString());
    }

    [Fact]
    public void Click_carries_only_the_object_selector()
    {
        var click = Json(Command.Click("button[type=submit]"));

        Assert.Equal("click", click.GetProperty("action").GetString());
        Assert.Equal("button[type=submit]", click.GetProperty("object").GetString());
        Assert.Equal(JsonValueKind.Null, click.GetProperty("params").ValueKind);
    }

    [Fact]
    public void Wait_carries_the_delay_in_miliseconds()
    {
        var wait = Json(Command.Wait(1500));

        Assert.Equal("wait", wait.GetProperty("action").GetString());
        Assert.Equal(1500, wait.GetProperty("params").GetProperty("miliseconds").GetInt32());
    }

    [Theory]
    [InlineData("close")]
    [InlineData("recheck")]
    [InlineData("reload")]
    public void Close_recheck_and_reload_carry_nothing(string expected)
    {
        var command = expected switch
        {
            "close" => Command.Close(),
            "recheck" => Command.Recheck(),
            _ => Command.Reload(),
        };

        var json = Json(command);
        Assert.Equal(expected, json.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("object").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("params").ValueKind);
    }
}
