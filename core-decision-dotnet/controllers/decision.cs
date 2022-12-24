using Microsoft.AspNetCore.Mvc;

[Route("[controller]/[action]")]
public class DecisionController : Controller
{
    private readonly ILogger logger;

    public DecisionController(ILogger<DecisionController> logger)
    {
        this.logger = logger;
    }

    public IActionResult scopes()
    {
        return Ok();
    }
}