using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "LinkedIn";

        internal int Running { get; private set; } = -1;

        internal string Location { get; private set; } = "Netherlands";

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (Running == (int)settings.running) return;

                Running = (int)settings.running;
                Location = (string)settings.locations[Running];

                LinkedInPage.reg_search_location_url = new Regex(@$"(^|&)location={Location}(&|$)", RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(LinkedInPage));
        }
    }
}