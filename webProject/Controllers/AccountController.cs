using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using webProject.Models;
using Microsoft.AspNetCore.Http;
using webProject.Services;

namespace webProject.Controllers;

public class AccountController : Controller
{
    private readonly IRunIdProvider _runIdProvider;

    public AccountController(IRunIdProvider runIdProvider)
    {
        _runIdProvider = runIdProvider;
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Title"] = "Log In";
        var vm = new LoginViewModel { ReturnUrl = returnUrl };
        return View(vm);
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Log In";
            return View(model);
        }

        // Since DB/auth isn't implemented yet, just simulate success for non-empty credentials.
        if (!string.IsNullOrWhiteSpace(model.Email) && !string.IsNullOrWhiteSpace(model.Password))
        {
            // Create claims and sign in user using cookie authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, model.Email!),
                new Claim(ClaimTypes.Name, model.Email!)
            };

            // If user did not select RememberMe, add RunId claim to force logout after app restart
            if (!model.RememberMe)
            {
                claims.Add(new Claim("RunId", _runIdProvider.RunId));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            // Set theme cookie to dark so layout can apply dark theme by default after login
            Response.Cookies.Append("theme", "dark", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,
                Secure = false,
                SameSite = SameSiteMode.Lax
            });

            if (!string.IsNullOrEmpty(model.ReturnUrl))
                return LocalRedirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password (authentication not implemented). Enter any non-empty values.");
        ViewData["Title"] = "Log In";
        return View(model);
    }

    // GET: /Account/Register
    [HttpGet]
    public IActionResult Register()
    {
        ViewData["Title"] = "Register";
        var vm = new RegisterViewModel();
        return View(vm);
    }

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Register";
            return View(model);
        }

        // Placeholder: in the future save user to DB.
        // For now sign in the user automatically after registration.
        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, model.Email!),
                new Claim(ClaimTypes.Name, model.Email!)
            };

            // On registration assume non-persistent session (user didn't explicitly choose RememberMe)
            claims.Add(new Claim("RunId", _runIdProvider.RunId));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            // Set theme cookie to dark after registration as well
            Response.Cookies.Append("theme", "dark", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,
                Secure = false,
                SameSite = SameSiteMode.Lax
            });

            return RedirectToAction("Index", "Home");
        }

        TempData["Message"] = "Registration succeeded (DB save not implemented). Please log in.";
        return RedirectToAction("Login");
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // Remove theme cookie on logout
        Response.Cookies.Delete("theme");
        TempData["Message"] = "You have been logged out.";
        return RedirectToAction("Login");
    }
}
