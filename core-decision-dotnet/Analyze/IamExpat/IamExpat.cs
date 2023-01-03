using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpat : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "IamExpat";

        private string[] SearchSet = new string[] { "nl/career/jobs-netherlands" };

        internal string Search => SearchSet[RunningMethodIndex];

        public override int RunningMethodIndex { get; set; }

        public override string[] RunnableMethods => SearchSet;

        protected override void PrepareNewSettings(dynamic settings)
        {
            lock (@lock)
            {
                RunningMethodIndex = (int)settings.running;
                SearchSet = settings.searchs.ToObject<string[]>();

                if (RunningMethodIndex == (int)settings.running) return;

                IamExpatPage.reg_search_url = new Regex(@$"://[^/]*iamexpat\.{Search}", RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IamExpatPage));
        }
    }
}