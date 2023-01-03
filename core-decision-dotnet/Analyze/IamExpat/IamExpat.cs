using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpat : Agency
    {
        private static readonly object @lock = new();

        private int Running = 0;

        private string[] SearchSet = new string[] { "nl/career/jobs-netherlands" };

        public override string Name => "IamExpat";

        internal string Search => SearchSet[Running];

        public override string[] Runnables(out int current)
        {
            current = Running;
            return SearchSet.ToArray();
        }

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                if (Running == (int)settings.running) return;

                Running = (int)settings.running;
                SearchSet = (string[])settings.searchs;

                IamExpatPage.reg_search_url = new Regex(@$"://[^/]*iamexpat\.{Search}", RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IamExpatPage));
        }
    }
}