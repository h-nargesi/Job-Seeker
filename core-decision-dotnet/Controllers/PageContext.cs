namespace Photon.JobSeeker
{
    [Serializable]
    public class PageContext
    {
        public long? Trend { get; set; }
        public string? Agency { get; set; }
        public string? Url { get; set; }
        public string? Content { get; set; }

        public override string ToString()
        {
            return @$"trend-id: {Trend}, agency: {Agency}, url: ""{Url}""";
        }
    }
}