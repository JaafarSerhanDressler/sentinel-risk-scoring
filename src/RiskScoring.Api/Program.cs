using Microsoft.EntityFrameworkCore;
using RiskScoring.Api.Persistence;
using RiskScoring.Api.Services;
using RiskScoring.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddScoped<MlFabricService>();

// Optional PostgreSQL persistence — enabled only when a connection string is set
// (ConnectionStrings:Postgres, or a DATABASE_URL in postgres:// form from
// Render/Railway/Heroku). Otherwise the app runs on the in-memory store.
var pgConn = builder.Configuration.GetConnectionString("Postgres")
             ?? builder.Configuration["ConnectionStrings:Postgres"];
if (string.IsNullOrWhiteSpace(pgConn))
{
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(dbUrl) &&
        (dbUrl.StartsWith("postgres://") || dbUrl.StartsWith("postgresql://")))
    {
        pgConn = ConvertDatabaseUrl(dbUrl);
    }
}
if (!string.IsNullOrWhiteSpace(pgConn))
{
    builder.Configuration["ConnectionStrings:Postgres"] = pgConn; // so PersistenceService sees it
    builder.Services.AddDbContext<SentinelDbContext>(o => o.UseNpgsql(pgConn));
}
builder.Services.AddSingleton<PersistenceService>();

var app = builder.Build();

// Seed/load the database (no-op when Postgres isn't configured).
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<DataStore>();
    var persistence = scope.ServiceProvider.GetRequiredService<PersistenceService>();
    await persistence.InitializeAsync(store);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// ----- Auth (mocked, maps to the four roles) -----------------------------
api.MapPost("/auth/login", (LoginRequest req, DataStore store) =>
{
    var email = string.IsNullOrWhiteSpace(req.Email) ? "alex.monroe@easterseals.org" : req.Email;
    var name = NameFromEmail(email);
    var role =
        email.Contains("admin", StringComparison.OrdinalIgnoreCase) ? "Administrator" :
        email.Contains("sup", StringComparison.OrdinalIgnoreCase) ? "Supervisor" :
        email.Contains("clin", StringComparison.OrdinalIgnoreCase) ? "Clinical" :
        "Frontline";
    var user = new AuthUser
    {
        Name = name,
        Role = role,
        Email = email,
        Initials = InitialsFrom(name),
        Token = Guid.NewGuid().ToString("N")
    };
    store.AddLog(LogSeverity.Audit, "Auth", user.Email, "SignIn", $"{user.Name} signed in ({user.Role})");
    return Results.Ok(user);
});

// ----- Participants ------------------------------------------------------
api.MapGet("/participants", (DataStore store) => Results.Ok(store.Participants));

api.MapGet("/participants/{id}", (string id, DataStore store, MlFabricService ml) =>
{
    var p = store.Participants.FirstOrDefault(x => x.Id == id);
    if (p is null) return Results.NotFound();
    var (events, meds) = ScoreUtil.EventsFor(p);
    return Results.Ok(new ParticipantDetail
    {
        Participant = p,
        RiskEvents = events,
        MedicationEvents = meds,
        Trend = ml.TrendFor(p)
    });
});

// ----- Programmes --------------------------------------------------------
api.MapGet("/programmes", (DataStore store) => Results.Ok(store.Programmes));

api.MapGet("/programmes/{code}", (string code, DataStore store) =>
{
    var pg = store.Programmes.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
    if (pg is null) return Results.NotFound();
    var people = store.Participants.Where(p => p.ProgrammeCode == pg.Code)
        .OrderByDescending(p => p.OverallScore).ToList();
    return Results.Ok(new ProgrammeDetail { Programme = pg, Participants = people });
});

// ----- Assessments (ML Fabric re-score) ----------------------------------
api.MapPost("/assessments", async (ScoreRequest req, MlFabricService ml) =>
{
    var result = await ml.ScoreAsync(req);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

api.MapGet("/assessments", (DataStore store, int take = 0) =>
    Results.Ok(store.Assessments.Take(take <= 0 ? 20 : take)));

// ----- Fabric runs -------------------------------------------------------
api.MapGet("/runs", (DataStore store, int take = 0) =>
    Results.Ok(store.Runs.Take(take <= 0 ? 10 : take)));

// ----- Data sources ------------------------------------------------------
api.MapGet("/datasources", (DataStore store) => Results.Ok(store.DataSources));

// ----- Logs / audit ------------------------------------------------------
api.MapGet("/logs", (DataStore store, string? level, int take = 0) =>
{
    IEnumerable<LogEntry> q = store.Logs;
    if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<LogSeverity>(level, true, out var lvl))
        q = q.Where(l => l.Level == lvl);
    return Results.Ok(q.Take(take <= 0 ? 60 : take));
});

// ----- Dashboard aggregates ---------------------------------------------
api.MapGet("/dashboard/kpis", (DataStore store) =>
{
    var open = store.Participants.Where(p => p.AlertState != AlertState.Closed).ToList();
    var kpi = new KpiSummary
    {
        TotalParticipants = store.Participants.Count,
        TotalProgrammes = store.Programmes.Count,
        OpenAlerts = open.Count,
        Emergencies = open.Count(p => p.AlertClass == AlertClass.Emergency),
        HighBandCount = store.Participants.Count(p => p.OverallBand == RiskBand.High),
        AssessmentsToday = (int)store.Metrics.PredictionsToday,
        OpenAlertsTrend = 6.4,
        EmergenciesTrend = 12.0
    };
    return Results.Ok(kpi);
});

api.MapGet("/dashboard/risk-distribution", (DataStore store) =>
{
    var buckets = Enum.GetValues<RiskBand>()
        .Select(b => new RiskDistributionBucket { Band = b, Count = store.Participants.Count(p => p.OverallBand == b) })
        .ToList();
    return Results.Ok(buckets);
});

api.MapGet("/dashboard/throughput", () =>
{
    var rng = new Random(42);
    var labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    var points = labels.Select(l => new ThroughputPoint
    {
        Label = l,
        Assessments = rng.Next(480, 600),
        AvgLatencyMs = rng.Next(1400, 2200)
    }).ToList();
    return Results.Ok(points);
});

// ----- Admin -------------------------------------------------------------
api.MapGet("/admin/metrics", (DataStore store) => Results.Ok(store.Metrics));

app.MapFallbackToFile("index.html");

app.Run();

static string NameFromEmail(string email)
{
    var local = email.Split('@')[0].Replace('.', ' ').Replace('_', ' ');
    return string.Join(' ', local.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}

static string InitialsFrom(string name) =>
    string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p[0])).ToUpperInvariant();

static string ConvertDatabaseUrl(string url)
{
    // postgres://user:pass@host:port/db  ->  Npgsql keyword connection string
    try
    {
        var uri = new Uri(url);
        var parts = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(parts[0]);
        var pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        var db = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;
        return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }
    catch
    {
        return url;
    }
}
