using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpat : Agency
    {
        private static readonly object @lock = new();

        private string[] LocationUrls { get; set; } = new string[0];


        public override string Name => "IamExpat";

        public override string[] SearchingMethodTitles => LocationUrls;

        public override string BaseUrl => "https://iamexpat." + LocationUrls[RunningSearchingMethodIndex][0..2];

        public override string SearchLink => "https://iamexpat." + LocationUrls[RunningSearchingMethodIndex];

        public override Regex? JobAcceptabilityChecker => null;


        public override string GetMainHtml(string html) => IamExpatPageJob.GetHtmlContent(html);

        protected override void ChangeSettings(dynamic? settings)
        {
            lock (@lock)
            {
                if (settings == null)
                {
                    LocationUrls = new string[] { string.Empty };
                    RunningSearchingMethodIndex = 0;
                }
                else
                {
                    if (RunningSearchingMethodIndex == (int)settings.running) return;

                    LocationUrls = settings.urls.ToObject<string[]>();
                    RunningSearchingMethodIndex = (int)settings.running;

                    IamExpatPage.reg_search_url = new Regex(
                        @$"://[^/]*iamexpat\.{LocationUrls[RunningSearchingMethodIndex]}",
                        RegexOptions.IgnoreCase);
                }
            }
        }

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(IamExpatPage));
        }
    }
}