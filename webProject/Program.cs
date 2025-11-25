using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using webProject.Services;
using webProject.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add this before var app = builder.Build();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<IGaussianEliminationService, GaussianEliminationService>();

// Register RunId provider (unique per application run)
var runId = Guid.NewGuid().ToString();
builder.Services.AddSingleton<IRunIdProvider>(new RunIdProvider(runId));

// Configure cookie authentication (simple cookie auth for demo purposes)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "GaussAuth";
        options.Cookie.HttpOnly = true;
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Redirect accidental requests to the old static index path (e.g. "/src/WebApp/wwwroot/index.html")
// to the MVC root so users/IDE bookmarks don't hit a missing static file.
app.Use(async (context, next) =>
{
    var p = context.Request.Path.Value ?? string.Empty;
    if (p.Contains("wwwroot", StringComparison.OrdinalIgnoreCase) || p.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/");
        return;
    }

    await next();
});

app.UseStaticFiles();

app.UseRouting();

// Add authentication middleware before authorization
app.UseAuthentication();

// Middleware: ensure users who logged in without RememberMe are signed out after application restart
app.Use(async (context, next) =>
{
    var runProvider = context.RequestServices.GetService<IRunIdProvider>();
    if (runProvider != null && context.User?.Identity?.IsAuthenticated == true)
    {
        var claim = context.User.FindFirst("RunId");
        if (claim != null && claim.Value != runProvider.RunId)
        {
            // Sign out the user if their RunId doesn't match current run id
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect("/Account/Login");
            return;
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();