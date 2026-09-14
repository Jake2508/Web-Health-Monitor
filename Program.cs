using Microsoft.EntityFrameworkCore;
using WebsiteHealthMonitor.Components;
using WebsiteHealthMonitor.Data;
using WebsiteHealthMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database Setup
builder.Services.AddDbContextFactory<AppDbContext>(options => 
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=monitor.db"));

// Reusable HttpClient
builder.Services.AddHttpClient("health", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WebsiteHealthMonitor/1.0");
});

// Worker
builder.Services.AddHostedService<HealthCheckWorker>();


var app = builder.Build();

// Create or upgrade the database file on every start, then seed once
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<AppDbContext>>();

    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    if (!await db.Sites.AnyAsync())
    {
        db.Sites.AddRange(
            new MonitoredSite { Name = "Google",  Url = "https://www.google.com" },
            new MonitoredSite { Name = "GitHub",  Url = "https://github.com" },
            new MonitoredSite { Name = "Portfolio", Url = "https://jake-rose.com" },
            new MonitoredSite { Name = "Always fails", Url = "https://httpbin.org/status/500" });

        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Default HSTS value is 30 days
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
