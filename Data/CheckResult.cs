namespace WebsiteHealthMonitor.Data;

public class CheckResult
{
    public int Id { get; set; }

    public int MonitoredSiteId { get; set; }
    public MonitoredSite? Site { get; set; }

    // UTC - Convert to local time when displayed
    public DateTime CheckedAt { get; set; }

    // Null when request hasn't completed at all (DNS, TLS, Timeout)
    public int? StatusCode { get; set; }

    public bool IsUp { get; set; }
    public int ResponseTimeMs { get; set; }
    public string? Error { get; set; }
}