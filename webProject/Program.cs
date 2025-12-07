using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using webProject.Services;
using webProject.Data;
using webProject.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for nginx proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 64; // Increase max depth for nested objects
        options.JsonSerializerOptions.DefaultBufferSize = 16 * 1024; // 16KB buffer
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals; // Allow Infinity/NaN
    });

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add this before var app = builder.Build();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Data Protection to use PostgreSQL for key storage
// This ensures both servers can decrypt cookies created by either server
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("GaussSolver"); // Same name for all instances

// Register services
builder.Services.AddScoped<IGaussianEliminationService, GaussianEliminationService>();
builder.Services.AddScoped<ILUDecompositionService, LUDecompositionService>();
builder.Services.AddScoped<ICombinedMatrixService, CombinedMatrixService>();
builder.Services.AddSingleton<ITaskManager, TaskManager>();

// Add SignalR
builder.Services.AddSignalR();

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Allow HTTP for load balancing
        options.Cookie.SameSite = SameSiteMode.Lax; // Less strict for load balancing
        options.Cookie.Path = "/"; // Ensure cookie is valid for all paths
        
        // Remember Me settings
        options.ExpireTimeSpan = TimeSpan.FromDays(30); // Cookie lives 30 days
        options.SlidingExpiration = true; // Renew cookie on each request
        options.Cookie.MaxAge = TimeSpan.FromDays(30); // Browser keeps cookie 30 days
        options.Cookie.IsEssential = true; // Essential cookie for authentication
    });

var app = builder.Build();

// Use forwarded headers from nginx proxy (must be first)
app.UseForwardedHeaders();

// Display server info on startup
var serverName = app.Configuration["ServerInfo:ServerName"] ?? "UNKNOWN";
var serverPort = app.Configuration["ServerInfo:Port"] ?? "UNKNOWN";
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("╔════════════════════════════════════════════╗");
Console.WriteLine($"║  Server: {serverName,-30} ║");
Console.WriteLine($"║  Port:   {serverPort,-30} ║");
Console.WriteLine("╚════════════════════════════════════════════╝");
Console.ResetColor();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Remove HTTPS redirection for load balancing
// app.UseHttpsRedirection();

// Add server logging middleware early in the pipeline
app.UseServerLogging();

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

// Disabled RunId middleware for load balancing
// This middleware was causing issues with distributed authentication
// app.Use(async (context, next) => { ... });

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<webProject.Hubs.ProgressHub>("/progressHub");

app.Run();