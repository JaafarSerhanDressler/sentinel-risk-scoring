using RiskScoring.Api.Services;
using RiskScoring.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddScoped<MlFabricService>();

var app = builder.Build();

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
