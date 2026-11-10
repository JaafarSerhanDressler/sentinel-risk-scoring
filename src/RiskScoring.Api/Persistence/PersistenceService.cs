using Microsoft.EntityFrameworkCore;
using RiskScoring.Api.Services;
using RiskScoring.Shared.Models;

namespace RiskScoring.Api.Persistence;

/// <summary>
/// Bridges the in-memory <see cref="DataStore"/> and PostgreSQL. When a
/// "Postgres" connection string is configured it seeds the database from the
/// synthetic data on first run, loads it back on subsequent runs, and persists
/// re-scores. All operations are guarded so a database problem never breaks the
/// API — it simply continues on the in-memory store.
/// </summary>
public class PersistenceService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PersistenceService> _log;

    public bool Enabled { get; }

    public PersistenceService(IServiceScopeFactory scopes, IConfiguration config, ILogger<PersistenceService> log)
    {
        _scopes = scopes;
        _log = log;
        var cs = config.GetConnectionString("Postgres") ?? config["ConnectionStrings:Postgres"];
        Enabled = !string.IsNullOrWhiteSpace(cs);
    }

    public async Task InitializeAsync(DataStore store)
    {
        if (!Enabled) return;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            await db.Database.EnsureCreatedAsync();

            if (!await db.Participants.AnyAsync())
            {
                db.Programmes.AddRange(store.Programmes);
                db.Participants.AddRange(store.Participants);
                db.DataSources.AddRange(store.DataSources);
                db.FabricRuns.AddRange(store.Runs);
                db.Assessments.AddRange(store.Assessments);
                db.Logs.AddRange(store.Logs);
                db.Metrics.Add(store.Metrics);
                await db.SaveChangesAsync();
                _log.LogInformation("Seeded PostgreSQL from synthetic data ({Count} participants).", store.Participants.Count);
            }
            else
            {
                var participants = await db.Participants.AsNoTracking().ToListAsync();
                var programmes = await db.Programmes.AsNoTracking().ToListAsync();
                var sources = await db.DataSources.AsNoTracking().ToListAsync();
                var runs = await db.FabricRuns.AsNoTracking().ToListAsync();
                var assessments = await db.Assessments.AsNoTracking().OrderByDescending(a => a.StartedAt).ToListAsync();
                var logs = await db.Logs.AsNoTracking().OrderByDescending(l => l.Id).ToListAsync();
                var metrics = await db.Metrics.AsNoTracking().FirstOrDefaultAsync() ?? store.Metrics;
                var maxLog = logs.Count > 0 ? logs.Max(l => l.Id) : 0;
                store.Hydrate(participants, programmes, sources, runs, assessments, logs, metrics, maxLog);
                _log.LogInformation("Loaded {Count} participants from PostgreSQL.", participants.Count);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PostgreSQL unavailable — continuing on the in-memory store.");
        }
    }

    public async Task PersistScoreAsync(Participant participant, AssessmentRun run,
        IEnumerable<LogEntry> newLogs, IEnumerable<Programme> programmes)
    {
        if (!Enabled) return;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            db.Participants.Update(participant);
            db.Assessments.Add(run);
            foreach (var l in newLogs) db.Logs.Add(l);
            db.Programmes.UpdateRange(programmes);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to persist score to PostgreSQL.");
        }
    }
}
