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
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 64;
        options.JsonSerializerOptions.DefaultBufferSize = 16 * 1024;
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("GaussSolver"); 

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379");
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IRedisQueueService, RedisQueueService>();

builder.Services.AddScoped<IGaussianEliminationService, GaussianEliminationService>();
builder.Services.AddScoped<ILUDecompositionService, LUDecompositionService>();
builder.Services.AddScoped<ICombinedMatrixService, CombinedMatrixService>();
builder.Services.AddSingleton<ITaskManager, TaskManager>();

builder.Services.AddHostedService<MatrixWorker>();


builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.EndPoints.Add("localhost:6379");
        options.Configuration.AbortOnConnectFail = false;
    });

var runId = Guid.NewGuid().ToString();
builder.Services.AddSingleton<IRunIdProvider>(new RunIdProvider(runId));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "GaussAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; 
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/"; 
        
        // Remember Me settings
        options.ExpireTimeSpan = TimeSpan.FromDays(30); 
        options.SlidingExpiration = true; 
        options.Cookie.MaxAge = TimeSpan.FromDays(30); 
        options.Cookie.IsEssential = true; 
    });

var app = builder.Build();

app.UseForwardedHeaders();

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

app.UseServerLogging();

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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<webProject.Hubs.ProgressHub>("/progressHub");

app.Run();