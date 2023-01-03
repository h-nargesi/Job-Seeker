using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpat : Agency
    {
        private static readonly object @lock = new();

        private string[] SearchSet = new string[] { "nl/career/jobs-netherlands" };

        public override string Name => "IamExpat";

        public override int RunningMethodIndex { get; set; }

        public override string[] RunnableMethods => SearchSet;
        
        internal string Search => SearchSet[RunningMethodIndex];

        public override string SearchLink()
        {
            return "https://iamexpat." + Search;
        }

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