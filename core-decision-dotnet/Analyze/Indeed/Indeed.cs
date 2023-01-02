using Photon.JobSeeker.IamExpat;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    class Indeed : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "Indeed";

        internal int Running { get; private set; } = -1;

        internal string Search { get; private set; } = "nl/career/jobs-netherlands";

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (Running == (int)settings.running) return;

                Running = (int)settings.running;
                Search = (string)settings.searchs[Running];

                IndeedPage.reg_search_url = new Regex(@$"://[^/]*iamexpat\.{Search}", RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IndeedPage));
        }
    }
}