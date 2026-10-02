namespace Photon.JobSeeker;

public struct AgencyRegion
{
    public string Title { get; set; }

    public string Url { get; set; }

    public object? Params { get; set; }

    public bool? Enabled { get; set; }

    public static AgencyRegion Empty { get; } = new()
    {
        Title = string.Empty,
        Url = string.Empty,
        Params = null,
        Enabled = false,
    };
}