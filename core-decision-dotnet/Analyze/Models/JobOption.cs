using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Analyze.Models
{
    struct JobOption
    {
        public string Category { get; set; }

        public long Score { get; set; }

        public string Title { get; set; }

        public Regex Pattern { get; set; }

        public dynamic? Settings { get; set; }

        public override string ToString()
        {
            return $"({Score}) {Title}";
        }

        public string ToString(char state, long? score = null)
        {
            return $"({state}{score ?? Score}) {Title}";
        }
    }
}