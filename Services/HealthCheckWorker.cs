using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WebsiteHealthMonitor.Data;

namespace WebsiteHealthMonitor.Services;


public class HealthCheckWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly IHttpClientFactory _httpFactory;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<HealthCheckWorker> _logger;


    public HealthCheckWorker(IHttpClientFactory httpFactory, IDbContextFactory<AppDbContext> dbFactory, ILogger<HealthCheckWorker> logger)
    {
        _httpFactory = httpFactory;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            // First cycle runs immediately at startup rather than after a minute of an empty dashboard
            do
            {
                await RunOneCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch(OperationCanceledException)
        {
            // App is shutting down nothing to report
            _logger.LogInformation("App is closing down nothing to report...");
        }
    }

    private async Task RunOneCycleAsync(CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var sites = await db.Sites
                .Where(s => s.Enabled)
                .AsNoTracking()
                .ToListAsync(ct);

            foreach(var site in sites)
            {
                var result = await CheckAsync(site, ct);

                db.Results.Add(result);

                _logger.LogInformation(
                    "{Name} -> {Status} in {Ms}ms",
                    site.Name,
                    result.StatusCode?.ToString() ?? result.Error,
                    result.ResponseTimeMs);
            }

            await db.SaveChangesAsync(ct);

            // Limit data to Retention value i.e 7days  
            var cutoff = DateTime.UtcNow - Retention;
            await db.Results
                .Where(r => r.CheckedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }
        catch(OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch(Exception ex)
        {
            // Bad cycle(locked db, disk full, etc) log it, try again next tick
            _logger.LogError(ex, "Health check cycle failed");
        }
                
    }

    private async Task<CheckResult> CheckAsync(MonitoredSite site, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("health");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, site.Url);

            // Only care about status code so don't download whole page body
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            stopwatch.Stop();

            return new CheckResult
            {
                MonitoredSiteId = site.Id,
                CheckedAt = DateTime.UtcNow,
                StatusCode = (int)response.StatusCode,
                IsUp = response.IsSuccessStatusCode,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch(OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down, not a site failure
            throw;
        }
        catch(Exception ex)
        {
            stopwatch.Stop();

            // Down is normal outcome here not an exception
            return new CheckResult
            {
                MonitoredSiteId = site.Id,
                CheckedAt = DateTime.UtcNow,
                StatusCode = null,
                IsUp = false,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Error = Describe(ex)
            };
        }
    }


    /// <summary>
    /// Describes base exception messages and handles unknown ones 
    /// </summary>
    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException                                                           => "Timed Out",
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError } => "DNS Failed",
        HttpRequestException http                                                       => http.Message,
        _                                                                               => ex.GetBaseException().Message
    };
}
