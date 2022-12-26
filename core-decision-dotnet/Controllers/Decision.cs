using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class DecisionController : Controller
    {
        private readonly Analyzer analyzer;
        private readonly Database database;

        public DecisionController(Analyzer analyzer, Database database)
        {
            this.analyzer = analyzer;
            this.database = database;
        }

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

        [HttpGet("/")]
        public IActionResult Index()
        {
            var list = database.Job.Fetch(JobState.attention);

            return View("index.cshtml", list);
        }
    }
}