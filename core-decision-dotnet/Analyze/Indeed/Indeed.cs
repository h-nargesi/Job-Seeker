using Photon.JobSeeker.IamExpat;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    class Indeed : Agency
    {
        private static readonly object @lock = new();

        private string[] SearchDomainSet = new string[] { "https://au.indeed.com/" };

        public override string Name => "Indeed";

        public override int RunningMethodIndex { get; set; }

        public override string[] RunnableMethods => SearchDomainSet;

        internal string SearchDomain => SearchDomainSet[RunningMethodIndex];

        public override string SearchLink()
        {
            return SearchDomain + "jobs?q=developer";
        }

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (RunningMethodIndex == (int)settings.running) return;

                RunningMethodIndex = (int)settings.running;
                SearchDomainSet = settings.domains.ToObject<string[]>();
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IndeedPage));
        }
    }
}