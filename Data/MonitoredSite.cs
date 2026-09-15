namespace WebsiteHealthMonitor.Data;

public class MonitoredSite
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    // Every check ever recorded for this site
    public List<CheckResult> Results { get; set; } = new();
}