namespace WebsiteHealthMonitor.Data;

public class MonitoredSite
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public bool Enabled { get; set; }

    // Every check ever recorded for this site
    public List<CheckResult> Results { get; set; } = new();
}