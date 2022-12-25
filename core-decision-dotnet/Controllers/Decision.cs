using Microsoft.AspNetCore.Mvc;

[Route("[controller]/[action]")]
public class DecisionController : Controller
{
    private readonly Analyzer analyzer;
    private readonly ILogger logger;
    private readonly Database database;

    public DecisionController(ILogger<DecisionController> logger, Analyzer analyzer, Database database)
    {
        this.analyzer = analyzer;
        this.logger = logger;
        this.database = database;
    }

    public IActionResult Take([FromBody] long? trend, [FromBody] string agency, [FromBody] string url, [FromBody] string content)
    {
        logger.LogInformation($"trend: {trend}, agency: {agency}, url: {url}, content: {content}");

        var result = analyzer.Analyze(trend, agency, url, content);

        return Ok(new
        {
            result.Trend,
            result.Commands,
        });
    }

    public IActionResult Reload()
    {
        analyzer.ClearAgencies();

        return Ok();
    }

    public IActionResult Scopes()
    {
        var agencies = analyzer.Agencies.Values.Select(a => new
        {
            a.Name,
            a.Domain,
        });

        return Ok(agencies);
    }

    public IActionResult Index()
    {
        var list = database.Job.Fetch(JobState.attention);

        return View("index.cshtml", list);
    }
}