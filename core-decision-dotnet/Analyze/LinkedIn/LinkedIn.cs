using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        private static readonly object @lock = new();

        private string[] Locations { get; set; } = new string[0];


        public override string Name => "LinkedIn";

        public override string[] SearchingMethodTitles => Locations;

        internal string Location => Locations[RunningSearchingMethodIndex];

        public override string SearchLink => "https://www.linkedin.com/jobs/search/";

        public override Regex? JobAcceptabilityChecker => LinkedInPage.reg_job_no_longer_accepting;


        protected override void ChangeSettings(dynamic? settings)
        {
            lock (@lock)
            {
                if (settings == null)
                {
                    Locations = new string[] { string.Empty };
                    RunningSearchingMethodIndex = 0;
                }
                else
                {
                    if (RunningSearchingMethodIndex == (int)settings.running) return;

                    Locations = settings.locations.ToObject<string[]>();
                    RunningSearchingMethodIndex = (int)settings.running;

                    LinkedInPage.reg_search_location_url = new Regex(
                        @$"(^|&)location={Location}(&|$)",
                        RegexOptions.IgnoreCase);
                }
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(LinkedInPage));
        }
    }
}