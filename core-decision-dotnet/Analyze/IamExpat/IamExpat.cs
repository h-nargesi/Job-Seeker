using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpat : Agency
    {
        private static readonly object @lock = new();

        public override string Name => "IamExpat";

        public override int RunningMethodIndex { get; set; }

        public override string[] SearchingMethods { get; protected set; } = new string[] { "nl/career/jobs-netherlands" };

        public override string SearchLink()
        {
            return "https://iamexpat." + SearchingMethods[RunningMethodIndex];
        }

        protected override void ChangeSettings(dynamic settings)
        {
            lock (@lock)
            {
                RunningMethodIndex = (int)settings.running;
                SearchingMethods = settings.searchs.ToObject<string[]>();

                if (RunningMethodIndex == (int)settings.running) return;

                IamExpatPage.reg_search_url = new Regex(
                    @$"://[^/]*iamexpat\.{SearchingMethods[RunningMethodIndex]}", 
                    RegexOptions.IgnoreCase);
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IamExpatPage));
        }
    }
}