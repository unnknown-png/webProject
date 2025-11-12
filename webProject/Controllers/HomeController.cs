using Microsoft.AspNetCore.Mvc;

namespace webProject.Controllers;

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