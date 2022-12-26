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
        Link = 128,
        Log = 256,
        All = 511,
    }
}