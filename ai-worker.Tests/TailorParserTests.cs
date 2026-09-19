namespace AiWorker.Tests;

public sealed class TailorParserTests
{
    [Fact]
    public void Parses_valid_delta()
    {
        var delta = TailorParser.Parse(
            """{"keys": ["DOTNET", "SQL"], "notIncluded": ["#douran"], "included": [], "length": 2, "texts": {"title": "Senior .NET Engineer"}}""");

        Assert.NotNull(delta);
        Assert.Equal("Senior .NET Engineer", delta!["texts"]!["title"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""["array"]""")]
    [InlineData("""{"keys": 5, "notIncluded": [], "included": [], "length": 1, "texts": {}}""")]
    [InlineData("""{"keys": [], "notIncluded": [5], "included": [], "length": 1, "texts": {}}""")]
    [InlineData("""{"keys": [], "notIncluded": [], "included": [], "length": 3, "texts": {}}""")]
    [InlineData("""{"keys": [], "notIncluded": [], "included": [], "length": 1, "texts": {"title": 5}}""")]
    public void Rejects_malformed_delta(string content)
    {
        Assert.Throws<ModelOutputException>(() => TailorParser.Parse(content));
    }
}
