using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();