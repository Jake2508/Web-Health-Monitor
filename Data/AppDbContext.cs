using Microsoft.EntityFrameworkCore;


namespace WebsiteHealthMonitor.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MonitoredSite> Sites => Set<MonitoredSite>();
    public DbSet<CheckResult> Results => Set<CheckResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Allows only unique URLs (no duplicates)
        builder.Entity<MonitoredSite>()
            .HasIndex(s => s.Url)
            .IsUnique();

        // Latest Result for site X - the query the dashboard runs, so give database an index shaped for it
        builder.Entity<CheckResult>()
            .HasIndex(r => new { r.MonitoredSiteId, r.CheckedAt });

        // Delete a site, its history goes with it
        builder.Entity<CheckResult>()
            .HasOne(r => r.Site)
            .WithMany(s => s.Results)
            .HasForeignKey(r => r.MonitoredSiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}