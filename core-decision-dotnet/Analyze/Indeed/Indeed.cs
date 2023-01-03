using Photon.JobSeeker.IamExpat;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    class Indeed : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "Indeed";

        private string[] SearchDomainSet = new string[] { "https://au.indeed.com/" };

        internal int Running { get; private set; } = -1;

        public override int RunningMethodIndex { get; set; }

        public override string[] RunnableMethods => SearchDomainSet;

        internal string SearchDomain => SearchDomainSet[RunningMethodIndex];

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (Running == (int)settings.running) return;

                Running = (int)settings.running;
                SearchDomainSet = settings.domains.ToObject<string[]>();
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IndeedPage));
        }
    }
}