using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

public static class AuthOptions
{
    public const string CookieName = "js_auth";
    public const string ProtectorPurpose = "auth";
}

[Route("[controller]/[action]")]
public class AuthController(IDataProtectionProvider protection, IConfiguration configuration) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector(AuthOptions.ProtectorPurpose);

    [HttpGet]
    public IActionResult Login()
    {
        if (AuthDisabled) return Redirect("/");

        return View("~/views/login.cshtml", string.Empty);
    }

    [HttpPost]
    public IActionResult Login([FromForm] string? password)
    {
        var api_key = configuration["Auth:ApiKey"];

        if (string.IsNullOrEmpty(api_key)) return Redirect("/");

        if (string.IsNullOrEmpty(password) || !FixedTimeEquals(password, api_key))
        {
            Log.Warning("Failed login attempt from {0}", HttpContext.Connection.RemoteIpAddress);
            return View("~/views/login.cshtml", "Wrong password.");
        }

        Response.Cookies.Append(AuthOptions.CookieName, protector.Protect("ok"), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            // TODO: set Secure = true once the server runs behind TLS
        });

        return Redirect("/");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthOptions.CookieName);
        return Redirect("/auth/login");
    }

    private bool AuthDisabled => string.IsNullOrEmpty(configuration["Auth:ApiKey"]);

    public static bool FixedTimeEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}
