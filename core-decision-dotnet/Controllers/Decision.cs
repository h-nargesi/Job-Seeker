using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class DecisionController : Controller
    {
        private readonly Analyzer analyzer;

        public DecisionController(Analyzer analyzer) => this.analyzer = analyzer;

        [HttpPost]
        public IActionResult Take([FromBody] long? trend, [FromBody] string agency, [FromBody] string url, [FromBody] string content)
        {
            Log.Information($"trend: {trend}, agency: {agency}, url: {url}, content: {content}");

            var result = analyzer.Analyze(trend, agency, url, content);

            return Ok(new
            {
                result.Trend,
                result.Commands,
            });
        }

        [HttpPost]
        public IActionResult Reload()
        {
            analyzer.ClearAgencies();

            return Ok();
        }

        [HttpPost]
        public IActionResult Scopes()
        {
            var agencies = analyzer.Agencies.Values.Select(a => new
            {
                a.Name,
                a.Domain,
            });

            return Ok(agencies);
        }

        [HttpGet]
        public IActionResult Index()
        {
            var list = Database.Open().Job.Fetch(JobState.attention);

            return View("~/views/index.cshtml", list);
        }
    }
}