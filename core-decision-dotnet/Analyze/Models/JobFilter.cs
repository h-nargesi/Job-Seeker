namespace Photon.JobSeeker
{
    enum JobFilter
    {
        AgencyID = 1,
        Country = 2,
        Code = 4,
        Title = 8,
        State = 16,
        Score = 32,
        Url = 64,
        Html = 128,
        Content = 256,
        Link = 512,
        Log = 1024,
        Options = 2048,
        Tries = 4096,
        All = 8191,
        ModifiedOn = 8192,
    }
}