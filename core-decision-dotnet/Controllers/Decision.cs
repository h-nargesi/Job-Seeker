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
        public IActionResult Take([FromBody] PageContext? context)
        {
            if (context == null) return BadRequest();

            try
            {
                Log.Information(context.ToString());

                var result = analyzer.Analyze(context);

                return Ok(new
                {
                    result.Trend,
                    result.Commands,
                });
            }
            catch (BadJobRequest bd)
            {
                Log.Error(bd.Message);
                return BadRequest();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                throw;
            }
        }

        [HttpPost]
        public IActionResult Reload()
        {
            try
            {
                analyzer.ClearAgencies();

                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                throw;
            }
        }

        [HttpGet]
        public IActionResult Scopes()
        {
            try
            {
                var agencies = analyzer.Agencies.Values.Select(a => new
                {
                    a.Name,
                    a.Domain,
                });

                return Ok(agencies);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                throw;
            }
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                var list = Database.Open().Job.Fetch(JobState.attention);

                return View("~/views/index.cshtml", list);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                throw;
            }
        }
    }
}