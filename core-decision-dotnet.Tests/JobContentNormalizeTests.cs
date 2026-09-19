namespace Photon.JobSeeker.Tests;

public class JobContentNormalizeTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  a\n\n\tb  ", "a b")]
    [InlineData("same", "same")]
    public void Normalize_collapses_whitespace_and_trims(string? input, string expected)
    {
        Assert.Equal(expected, JobContent.Normalize(input));
    }

    [Fact]
    public void HasChanged_is_false_when_only_whitespace_differs()
    {
        Assert.False(JobContent.HasChanged("hello   world", "hello world"));
        Assert.False(JobContent.HasChanged("  hello world\n", "hello world"));
    }

    [Fact]
    public void HasChanged_is_true_when_text_differs()
    {
        Assert.True(JobContent.HasChanged("hello world", "hello worlds"));
        Assert.True(JobContent.HasChanged(null, "new listing"));
        Assert.False(JobContent.HasChanged(null, "   "));
    }

    [Fact]
    public void Fingerprint_is_sha256_of_normalized_text()
    {
        var a = JobContent.Fingerprint("hello   world");
        var b = JobContent.Fingerprint("hello world");
        var c = JobContent.Fingerprint("hello worlds");

        Assert.Equal(64, a.Length);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(JobContent.Fingerprint(null), JobContent.Fingerprint(""));
    }
}
