using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        private static readonly object @lock = new();

        private string[] LocationSet = new string[] { "Netherlands" };

        public override string Name => "LinkedIn";

        public override int RunningMethodIndex { get; set; }

        public override string[] RunnableMethods => LocationSet;

        internal string Location => LocationSet[RunningMethodIndex];

        public override string SearchLink()
        {
            return "https://www.linkedin.com/jobs/search/";
        }

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                RunningMethodIndex = (int)settings.running;
                LocationSet = settings.locations.ToObject<string[]>();

                if (RunningMethodIndex == (int)settings.running) return;

                LinkedInPage.reg_search_location_url = new Regex(@$"(^|&)location={Location}(&|$)", RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(LinkedInPage));
        }
    }
}