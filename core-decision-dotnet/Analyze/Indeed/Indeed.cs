using Photon.JobSeeker.IamExpat;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    class Indeed : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "Indeed";

        internal int Running { get; private set; } = -1;

        internal string SearchDomain { get; private set; } = "https://au.indeed.com/";

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (Running == (int)settings.running) return;

                Running = (int)settings.running;
                SearchDomain = (string)settings.domains[Running];
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IndeedPage));
        }
    }
}