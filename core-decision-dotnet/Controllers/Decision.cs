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
                    trend = result.TrendID,
                    commands = result.Commands,
                });
            }
            catch (BadJobRequest bd)
            {
                Log.Error(bd.Message);
                return BadRequest();
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        [HttpPost]
        public IActionResult Reload()
        {
            try
            {
                using var database = Database.Open();
                database.Trend.DeleteExpired(0);
                analyzer.ClearAgencies();

                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
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
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        [HttpGet]
        public IActionResult Orders()
        {
            try
            {
                using var trends_checkpoint = new TrendsCheckpoint(analyzer);
                var result = trends_checkpoint.CheckCurrentTrends();
                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }
    }
}