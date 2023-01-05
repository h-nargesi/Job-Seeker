using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "LinkedIn";

        public override int RunningMethodIndex { get; set; }

        public override string[] SearchingMethods { get; protected set; } = new string[] { "Netherlands" };

        internal string Location => SearchingMethods[RunningMethodIndex];

        public override string SearchLink()
        {
            return "https://www.linkedin.com/jobs/search/";
        }

        protected override void ChangeSettings(dynamic settings)
        {
            lock (@lock)
            {
                RunningMethodIndex = (int)settings.running;
                SearchingMethods = settings.locations.ToObject<string[]>();

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