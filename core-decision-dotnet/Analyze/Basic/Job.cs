class Job
{
    public long JobID { get; set; }

    public long AgencyID { get; set; }

    public string? Url { get; set; }

    public string? Code { get; set; }

    public string? Html { get; set; }

    public string? Title { get; set; }

    public JobState State { get; set; }

    public string? Log { get; set; }
}