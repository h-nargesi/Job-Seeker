using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "LinkedIn";

        private int Running = 0;

        private string[] LocationSet = new string[] { "Netherlands" };

        internal string Location => LocationSet[Running];

        public override string[] Runnables(out int current)
        {
            current = Running;
            return LocationSet.ToArray();
        }

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (Running == (int)settings.running) return;

                Running = (int)settings.running;
                LocationSet = (string[])settings.locations;

                LinkedInPage.reg_search_location_url = new Regex(@$"(^|&)location={Location}(&|$)", RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(LinkedInPage));
        }
    }
}