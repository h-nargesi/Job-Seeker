using System.Text.RegularExpressions;

namespace Photon.JobSeeker
{
    struct JobOption
    {
        public long Score { get; set; }

        public string Title { get; set; }

        public Regex Pattern { get; set; }

        public string Options { get; set; }

        public override string ToString()
        {
            return $"({Score}) {Title}";
        }
    }
}