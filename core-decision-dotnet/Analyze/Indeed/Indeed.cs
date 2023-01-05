using Photon.JobSeeker.IamExpat;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    class Indeed : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "Indeed";

        public override int RunningMethodIndex { get; set; }

        public override string[] SearchingMethods { get; protected set; } = new string[] { "https://au.indeed.com/" };

        public override string SearchLink()
        {
            return SearchingMethods[RunningMethodIndex] + "jobs?q=developer";
        }

        protected override void ChangeSettings(dynamic settings)
        {
            lock (@lock)
            {
                RunningMethodIndex = (int)settings.running;
                SearchingMethods = settings.domains.ToObject<string[]>();
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IndeedPage));
        }
    }
}