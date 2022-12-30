namespace Photon.JobSeeker
{
    enum JobFilter
    {
        AgencyID = 1,
        Code = 2,
        Title = 4,
        State = 8,
        Score = 16,
        Url = 32,
        Html = 64,
        Content = 128,
        Link = 256,
        Log = 512,
        Tries = 1024,
        All = 2047,
    }
}