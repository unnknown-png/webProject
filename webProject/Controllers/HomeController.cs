using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace webProject.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    // Simple error action placeholder
    public IActionResult Error()
    {
        return Problem("An error occurred.");
    }
}