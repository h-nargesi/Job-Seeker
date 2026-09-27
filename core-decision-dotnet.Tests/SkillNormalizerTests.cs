namespace Photon.JobSeeker.Tests;

public class SkillNormalizerTests
{
    private static readonly IReadOnlyDictionary<string, string> NoAliases =
        new Dictionary<string, string>();

    [Theory]
    [InlineData("  JavaScript  ", "javascript")]
    [InlineData("CI/CD", "ci cd")]
    [InlineData("Node.js", "node.js")]
    [InlineData("C++", "c++")]
    [InlineData("R&D", "r&d")]
    [InlineData("front-end", "front end")]
    [InlineData("FRONT_END", "front end")]
    [InlineData("!!!", "")]
    [InlineData("", "")]
    public void Normalize_cleans_and_folds_plain_phrases(string phrase, string expected)
    {
        Assert.Equal(expected, SkillNormalizer.Normalize(phrase, NoAliases));
    }

    [Theory]
    [InlineData("REST APIs", "rest api")]
    [InlineData("networks", "network")]
    [InlineData("databases", "database")]
    [InlineData("repositories", "repository")]
    [InlineData("processes", "process")]
    [InlineData("boxes", "box")]
    [InlineData("css", "css")]
    [InlineData("redis", "redis")]
    [InlineData("analysis", "analysis")]
    [InlineData("business", "business")]
    public void Normalize_folds_plurals_conservatively(string phrase, string expected)
    {
        Assert.Equal(expected, SkillNormalizer.Normalize(phrase, NoAliases));
    }

    [Fact]
    public void Normalize_applies_alias_map_after_folding()
    {
        var aliases = new Dictionary<string, string> { ["js"] = "JavaScript", ["reactj"] = "react" };

        Assert.Equal("javascript", SkillNormalizer.Normalize("JS", aliases));
        Assert.Equal("react", SkillNormalizer.Normalize("ReactJS", aliases));
        Assert.Equal("golang", SkillNormalizer.Normalize("golang", aliases));
    }

    [Fact]
    public void Normalize_applies_seed_aliases()
    {
        var aliases = SkillNormalizer.BuildAliasMap(SkillNormalizer.SeedAliases);

        Assert.Equal("c#", SkillNormalizer.Normalize("C#.NET", aliases));
        Assert.Equal(".net", SkillNormalizer.Normalize("dotnet", aliases));
        Assert.Equal("kubernetes", SkillNormalizer.Normalize("kubernete", aliases));
        Assert.Equal("jenkins", SkillNormalizer.Normalize("jenkins", aliases));
    }

    [Fact]
    public void BuildAliasMap_normalizes_keys_and_lowercases_values()
    {
        var map = SkillNormalizer.BuildAliasMap([new KeyValuePair<string, string>("K8S", "Kubernetes")]);

        var (key, value) = Assert.Single(map);
        Assert.Equal("k8s", key);
        Assert.Equal("kubernetes", value);
    }

    [Fact]
    public void KnownSkills_collects_master_resume_key_names_and_phrases()
    {
        var aliases = SkillNormalizer.BuildAliasMap(SkillNormalizer.SeedAliases);
        var resume = ResumeHtml.MasterContext();
        resume.Keys["DOTNET"]!.Add("ASP.NET Core");

        var known = SkillNormalizer.KnownSkills(resume, aliases);

        Assert.Contains(".net", known);
        Assert.Contains("asp.net core", known);
        Assert.Contains("java", known);
        Assert.Contains("machine learning", known);
        Assert.Contains("front end", known);
    }
}
