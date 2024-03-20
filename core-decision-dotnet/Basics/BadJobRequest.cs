namespace Photon.JobSeeker;

public class BadJobRequest : Exception
{
    public BadJobRequest(string message) : base(message) { }
}