using Microsoft.AspNetCore.Mvc;

[Route("[controller]/[action]")]
public class DecisionController : Controller
{
    private readonly Analyzer analyzer;
    private readonly ILogger logger;

    public DecisionController(ILogger<DecisionController> logger, Analyzer analyzer)
    {
        this.analyzer = analyzer;
        this.logger = logger;
    }

    public IActionResult Take([FromBody] string agency, [FromBody] string url, [FromBody] string content)
    {
        logger.LogInformation($"agency: {agency}, url: {url}, content: {content}");

        var result = analyzer.Analyze(agency, url, content);

        return Ok(result.Commands);
    }

    public IActionResult Reload()
    {
        analyzer.LoadAgencies();

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
}