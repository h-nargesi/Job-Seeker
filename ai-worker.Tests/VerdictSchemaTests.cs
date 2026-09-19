using System.Text.Json;

namespace AiWorker.Tests;

public sealed class VerdictSchemaTests
{
    private static JsonElement Schema()
    {
        var format = JsonDocument.Parse(VerdictSchema.ResponseFormat).RootElement;
        return format.GetProperty("json_schema").GetProperty("schema");
    }

    private static JsonElement Property(string name)
    {
        return Schema().GetProperty("properties").GetProperty(name);
    }

    [Fact]
    public void ResponseFormatTargetsJsonSchemaStrictly()
    {
        var format = JsonDocument.Parse(VerdictSchema.ResponseFormat).RootElement;

        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.Equal("job_verdict", format.GetProperty("json_schema").GetProperty("name").GetString());
        Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public void SchemaIsClosedObject()
    {
        var schema = Schema();

        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void EveryPropertyIsRequired()
    {
        var schema = Schema();
        var properties = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
        var required = schema.GetProperty("required").EnumerateArray().Select(r => r.GetString()!)
            .OrderBy(n => n).ToList();

        Assert.Equal(properties, required);
    }

    [Fact]
    public void VerdictEnumExcludesError()
    {
        var verdict = Property("verdict");

        Assert.Equal(VerdictParser.ModelVerdicts,
            verdict.GetProperty("enum").EnumerateArray().Select(v => v.GetString()!).ToArray());
        Assert.DoesNotContain("Error", VerdictParser.ModelVerdicts);
    }

    [Fact]
    public void ExtractionEnumsMatchLockedSets()
    {
        Assert.Equal(["Junior", "Mid", "Senior", "Lead", "Unknown"],
            Property("seniority").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["Hour", "Day", "Month", "Year", "Unknown"],
            Property("period").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["Onsite", "Hybrid", "Remote", "Unknown"],
            Property("work_model").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["Permanent", "B2B", "Temporary", "Unknown"],
            Property("contract").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public void ScoreFieldsAreBounded()
    {
        var relevance = Property("relevance");
        Assert.Equal(0, relevance.GetProperty("minimum").GetInt32());
        Assert.Equal(100, relevance.GetProperty("maximum").GetInt32());

        var experience = Property("experience_years");
        Assert.Equal(0, experience.GetProperty("minimum").GetInt32());
        Assert.Equal(50, experience.GetProperty("maximum").GetInt32());
    }
}
