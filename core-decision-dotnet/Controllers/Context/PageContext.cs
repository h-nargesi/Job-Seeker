using System.Text.Json.Serialization;

namespace Photon.JobSeeker
{
    [Serializable]
    public class PageContext
    {
        public long? Trend { get; set; }
        public string? Agency { get; set; }
        public string? Url { get; set; }
        public string? Content { get; set; }
        public bool Challenge { get; set; }

        [JsonPropertyName("challenge_kind")]
        public string? ChallengeKind { get; set; }

        public override string ToString()
        {
            return @$"trend-id: {Trend?.ToString() ?? "?"}, agency: {Agency ?? "?"}, url: {Url ?? "?"}, challenge: {Challenge}, challenge-kind: {ChallengeKind ?? "?"}";
        }
    }
}