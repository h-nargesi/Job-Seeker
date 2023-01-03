using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "LinkedIn";

        private string[] LocationSet = new string[] { "Netherlands" };

        public override int RunningMethodIndex { get; set; }

        public override string[] RunnableMethods => LocationSet;

        internal string Location => LocationSet[RunningMethodIndex];

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