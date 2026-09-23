namespace AiWorker.Tests;

public sealed class VerdictParserTests
{
    private const string Valid = """
    {
      "relevance": 82,
      "verdict": "Match",
      "reason": "Genuine senior .NET role",
      "seniority": "Senior",
      "salary_min": 60000,
      "salary_max": 80000,
      "currency": "EUR",
      "period": "Year",
      "work_model": "Hybrid",
      "relocation_support": "Yes",
      "contract": "Permanent",
      "experience_years": 5,
      "skills": [".NET", "C#", "Azure"]
    }
    """;

    [Fact]
    public void ValidOutputParsesWithEchoedJobFields()
    {
        var payload = VerdictParser.Parse(Valid, 12, "fp1");

        Assert.Equal(12, payload.JobId);
        Assert.Equal(82, payload.Relevance);
        Assert.Equal("Match", payload.Verdict);
        Assert.Equal("Senior", payload.Seniority);
        Assert.Equal(60000, payload.SalaryMin);
        Assert.Equal(80000, payload.SalaryMax);
        Assert.Equal("EUR", payload.Currency);
        Assert.Equal("Year", payload.Period);
        Assert.Equal("Hybrid", payload.WorkModel);
        Assert.Equal("Yes", payload.RelocationSupport);
        Assert.Equal("Permanent", payload.Contract);
        Assert.Equal(5, payload.ExperienceYears);
        Assert.Equal([".NET", "C#", "Azure"], payload.Skills);
        Assert.Equal("fp1", payload.Fingerprint);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"relevance": 101, "verdict": "Match", "reason": "x", "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Error", "reason": "x", "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "match", "reason": "x", "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Match", "reason": "x", "seniority": "Principal", "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Match", "reason": "x", "work_model": "WFH", "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Match", "reason": "x", "relocation_support": "Maybe", "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Match", "reason": "x", "salary_min": -1, "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Match", "reason": "x", "salary_min": 90000, "salary_max": 60000, "skills": []}""")]
    [InlineData("""{"relevance": 82, "verdict": "Match", "reason": "x", "experience_years": 51, "skills": []}""")]
    [InlineData("""{"verdict": "Match", "reason": "x", "skills": []}""")]
    public void InvalidOutputFails(string content)
    {
        Assert.Throws<ModelOutputException>(() => VerdictParser.Parse(content, 1, "fp"));
    }

    [Fact]
    public void NullOptionalsAreAccepted()
    {
        const string content = """
            {"relevance": 40, "verdict": "Possible", "reason": null, "seniority": null,
             "salary_min": null, "salary_max": null, "currency": null, "period": "Unknown",
             "work_model": null, "relocation_support": "Unknown", "contract": null,
             "experience_years": null, "skills": null}
            """;

        var payload = VerdictParser.Parse(content, 3, "fp");

        Assert.Null(payload.Reason);
        Assert.Null(payload.Seniority);
        Assert.Null(payload.SalaryMin);
        Assert.Equal("Unknown", payload.Period);
        Assert.Equal("Unknown", payload.RelocationSupport);
        Assert.Null(payload.Skills);
    }

    [Fact]
    public void ReasonAndSkillsAreCapped()
    {
        var longReason = new string('r', VerdictPayload.ReasonMaxLength + 500);
        var manySkills = Enumerable.Range(0, 40).Select(i => $"skill{i}").ToArray();
        var content = $$"""
            {"relevance": 10, "verdict": "NoMatch", "reason": "{{longReason}}",
             "skills": [{{string.Join(",", manySkills.Select(s => $"\"{s}\""))}}]}
            """;

        var payload = VerdictParser.Parse(content, 4, "fp");

        Assert.Equal(VerdictPayload.ReasonMaxLength, payload.Reason!.Length);
        Assert.Equal(VerdictPayload.SkillsMaxCount, payload.Skills!.Count);
    }

    [Fact]
    public void ErrorVerdictCarriesFingerprintAndTruncatedReason()
    {
        var payload = VerdictPayload.Error(9, "fp9", new string('e', VerdictPayload.ReasonMaxLength));

        Assert.Equal(0, payload.Relevance);
        Assert.Equal("Error", payload.Verdict);
        Assert.Equal("fp9", payload.Fingerprint);
        Assert.Equal(VerdictPayload.ReasonMaxLength, payload.Reason!.Length);
    }
}
