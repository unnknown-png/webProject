using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using webProject.Models;
using webProject.Services;
using webProject.Data;
using Microsoft.EntityFrameworkCore;
using webProject.Helpers;

namespace webProject.Controllers;

public class AccountController : Controller
{
    private readonly IRunIdProvider _runIdProvider;
    private readonly ApplicationDbContext _context;

    public AccountController(IRunIdProvider runIdProvider, ApplicationDbContext context)
    {
        _runIdProvider = runIdProvider;
        _context = context;
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

        // Find user in database
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        
        if (user != null && VerifyPassword(model.Password!, user.PasswordHash))
        {
            // Update LastLogin timestamp
            user.LastLogin = TimeZoneHelper.UtcNow;
            await _context.SaveChangesAsync();
            
            // Create claims and sign in user using cookie authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email)
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
                IsPersistent = model.RememberMe, // true = cookie survives browser close
                AllowRefresh = true, // Allow cookie to refresh
                ExpiresUtc = model.RememberMe 
                    ? DateTimeOffset.UtcNow.AddDays(30) 
                    : DateTimeOffset.UtcNow.AddMinutes(30) // Session expires in 30 min if not RememberMe
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

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
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

        // Check if user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(string.Empty, "User with this email already exists.");
            ViewData["Title"] = "Register";
            return View(model);
        }

        // Create new user
        var user = new User
        {
            Email = model.Email!,
            PasswordHash = HashPassword(model.Password!),
            CreatedAt = TimeZoneHelper.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Sign in the user automatically after registration
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email)
        };

        // On registration assume non-persistent session (user didn't explicitly choose RememberMe)
        claims.Add(new Claim("RunId", _runIdProvider.RunId));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false, // Session cookie for registration
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) // Short session for new users
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

    // Simple password hashing using BCrypt-style approach
    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
