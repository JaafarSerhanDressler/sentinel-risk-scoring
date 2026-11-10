using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RiskScoring.Shared.Models;

namespace RiskScoring.Api.Persistence;

/// <summary>
/// EF Core context persisting the Sentinel domain to PostgreSQL. Used only when
/// a "Postgres" connection string is configured; otherwise the app runs on the
/// in-memory synthetic store.
/// </summary>
public class SentinelDbContext : DbContext
{
    public SentinelDbContext(DbContextOptions<SentinelDbContext> options) : base(options) { }

    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Programme> Programmes => Set<Programme>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<FabricRun> FabricRuns => Set<FabricRun>();
    public DbSet<AssessmentRun> Assessments => Set<AssessmentRun>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<ModelMetrics> Metrics => Set<ModelMetrics>();

    private static readonly JsonSerializerOptions Json = new();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var participant = b.Entity<Participant>();
        participant.HasKey(p => p.Id);
        // Nested risk-type scores stored as a JSON string column.
        participant.Property(p => p.RiskScores)
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<RiskTypeScore>>(v, Json) ?? new List<RiskTypeScore>())
            .Metadata.SetValueComparer(new ValueComparer<List<RiskTypeScore>>(
                (a, c) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(c, Json),
                v => JsonSerializer.Serialize(v, Json).GetHashCode(),
                v => JsonSerializer.Deserialize<List<RiskTypeScore>>(JsonSerializer.Serialize(v, Json), Json)!));

        b.Entity<Programme>().HasKey(p => p.Code);
        b.Entity<DataSource>().HasKey(d => d.Key);
        b.Entity<FabricRun>().HasKey(r => r.RunId);
        b.Entity<AssessmentRun>().HasKey(a => a.Id);

        var log = b.Entity<LogEntry>();
        log.HasKey(l => l.Id);
        log.Property(l => l.Id).ValueGeneratedNever(); // ids assigned by the app

        b.Entity<ModelMetrics>().HasKey(m => m.ModelVersion);

        base.OnModelCreating(b);
    }
}
