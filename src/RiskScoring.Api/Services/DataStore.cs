using RiskScoring.Shared.Models;

namespace RiskScoring.Api.Services;

/// <summary>
/// In-memory, thread-safe store standing in for the app's PostgreSQL + the
/// Fabric non-production workspace. Seeded with SYNTHETIC participants across
/// BH RES, ICMS, SH and SH SUBONLY, plus the daily Fabric runs, data-source
/// telemetry and an append-only audit log. Includes every required case from
/// the discovery spec (emergencies, no case manager, insufficient data, a long
/// refusal streak, hospitalised, risk dropped below alert, a failed/late run).
/// </summary>
public class DataStore
{
    private readonly object _gate = new();
    private long _logId;

    public List<Participant> Participants { get; } = new();
    public List<Programme> Programmes { get; } = new();
    public List<DataSource> DataSources { get; } = new();
    public List<FabricRun> Runs { get; } = new();
    public List<AssessmentRun> Assessments { get; } = new();
    public List<LogEntry> Logs { get; } = new();
    public ModelMetrics Metrics { get; private set; } = new();

    public DataStore() => Seed();

    public LogEntry AddLog(LogSeverity level, string category, string actor, string action,
        string message, string? participantId = null, string? correlation = null)
    {
        lock (_gate)
        {
            var entry = new LogEntry
            {
                Id = ++_logId,
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Actor = actor,
                Action = action,
                Message = message,
                ParticipantId = participantId,
                Correlation = correlation ?? Guid.NewGuid().ToString("N")[..8]
            };
            Logs.Insert(0, entry);
            if (Logs.Count > 500) Logs.RemoveAt(Logs.Count - 1);
            return entry;
        }
    }

    public void AddAssessment(AssessmentRun run)
    {
        lock (_gate)
        {
            Assessments.Insert(0, run);
            if (Assessments.Count > 200) Assessments.RemoveAt(Assessments.Count - 1);
            Metrics.PredictionsToday++;
        }
    }

    public void RecomputeProgrammes()
    {
        foreach (var pg in Programmes)
        {
            var people = Participants.Where(p => p.ProgrammeCode == pg.Code).ToList();
            pg.ParticipantCount = people.Count;
            pg.OpenAlerts = people.Count(p => p.AlertState != AlertState.Closed);
            pg.Emergencies = people.Count(p => p.AlertClass == AlertClass.Emergency && p.AlertState != AlertState.Closed);
            pg.HighBandCount = people.Count(p => p.OverallBand == RiskBand.High);
            pg.AverageScore = people.Count == 0 ? 0 : Math.Round(people.Average(p => p.OverallScore), 0);
        }
    }

    private void Seed()
    {
        var credibleAsOf = DateTime.UtcNow.AddHours(-6);
        var chartMedsAsOf = DateTime.UtcNow.AddHours(-9);

        Programmes.AddRange(new[]
        {
            new Programme { Code = "BH RES", Name = "Behavioral Health — Residential", ServiceLine = ServiceLine.Residential },
            new Programme { Code = "ICMS", Name = "Intensive Case Management", ServiceLine = ServiceLine.CaseManagement },
            new Programme { Code = "SH", Name = "Supportive Housing", ServiceLine = ServiceLine.SupportiveHousing },
            new Programme { Code = "SH SUBONLY", Name = "Supportive Housing — Substance Only", ServiceLine = ServiceLine.SupportiveHousing },
        });

        DataSources.AddRange(new[]
        {
            new DataSource { Key = "credible", Name = "Credible EHR", Type = DataSourceType.Credible, Status = DataSourceStatus.Connected, LastSync = credibleAsOf, RecordsIngested = 1_204_880, LatencyMs = 48, Quality = 0.96, Description = "Clients, episodes, events (1180), reports" },
            new DataSource { Key = "chartmeds", Name = "ChartMeds", Type = DataSourceType.ChartMeds, Status = DataSourceStatus.Degraded, LastSync = chartMedsAsOf, RecordsIngested = 402_551, LatencyMs = 260, Quality = 0.82, Description = "Medication administration & missed doses" },
            new DataSource { Key = "fabric", Name = "Fabric Pipeline", Type = DataSourceType.Fabric, Status = DataSourceStatus.Connected, LastSync = DateTime.UtcNow.AddHours(-5), RecordsIngested = 98_430, LatencyMs = 1_760, Quality = 0.94, Description = "Daily risk snapshots via SQL endpoint" },
            new DataSource { Key = "dayforce", Name = "Dayforce Roster", Type = DataSourceType.Dayforce, Status = DataSourceStatus.Degraded, LastSync = DateTime.UtcNow.AddHours(-14), RecordsIngested = 54_770, LatencyMs = 210, Quality = 0.64, Description = "Shifts & manager hierarchy (site/supervisor gap)" },
            new DataSource { Key = "entra", Name = "Microsoft Entra ID", Type = DataSourceType.Entra, Status = DataSourceStatus.Connected, LastSync = DateTime.UtcNow.AddMinutes(-20), RecordsIngested = 1_204, LatencyMs = 90, Quality = 0.99, Description = "Sign-in, roles, test users" },
            new DataSource { Key = "notifications", Name = "Push / Email", Type = DataSourceType.Notifications, Status = DataSourceStatus.Connected, LastSync = DateTime.UtcNow.AddMinutes(-3), RecordsIngested = 77_905, LatencyMs = 120, Quality = 0.97, Description = "Web push, browser & email delivery" },
        });

        Metrics = new ModelMetrics
        {
            ModelVersion = "risk-fabric-2026.11.02",
            Algorithm = "Gradient-Boosted Ensemble + Temporal Attention",
            Auc = 0.907,
            Accuracy = 0.864,
            Precision = 0.828,
            Recall = 0.791,
            Drift = 0.09,
            LastTrained = DateTime.UtcNow.AddDays(-11),
            PredictionsToday = 561,
            AvgLatencyMs = 1_760,
            Uptime = 0.9992,
            CredibleAsOf = credibleAsOf,
            ChartMedsAsOf = chartMedsAsOf
        };

        // Seed daily Fabric runs (one failed, one late).
        for (int d = 7; d >= 0; d--)
        {
            var started = DateTime.UtcNow.Date.AddDays(-d).AddHours(5); // 01:00 ET ~ 05:00 UTC
            var status = d == 3 ? RunStatus.Failed : d == 5 ? RunStatus.Late : (d == 0 ? RunStatus.Completed : RunStatus.Completed);
            Runs.Add(new FabricRun
            {
                RunId = started.ToString("yyyy-MM-ddTHH:mm'Z'"),
                ModelVersion = Metrics.ModelVersion,
                StartedAt = started,
                FinishedAt = status == RunStatus.Failed ? null : started.AddMinutes(status == RunStatus.Late ? 92 : Random.Shared.Next(18, 34)),
                Status = status,
                ParticipantRows = status == RunStatus.Failed ? 0 : 561,
                EventRows = status == RunStatus.Failed ? 0 : Random.Shared.Next(240, 420),
                MedicationEventRows = status == RunStatus.Failed ? 0 : Random.Shared.Next(120, 260),
                CredibleAsOf = started.AddHours(-1),
                ChartMedsAsOf = started.AddHours(-3)
            });
        }
        Runs.Reverse(); // newest first

        SeedParticipants();
        RecomputeProgrammes();

        AddLog(LogSeverity.Success, "Ingestion", "fabric@pipeline", "RunCompleted",
            $"Fabric run {Runs[0].RunId} promoted — 561 participants, risk as of {credibleAsOf:MMM d HH:mm} UTC");
        AddLog(LogSeverity.Error, "Ingestion", "fabric@pipeline", "RunFailed",
            "A Fabric run failed validation — previous run kept live; no participant rows promoted");
        AddLog(LogSeverity.Warning, "Ingestion", "fabric@pipeline", "RunLate",
            "A Fabric run finished after the 05:00 ET window (late)");
        AddLog(LogSeverity.Warning, "CareOps", "rules-engine", "EscalationTriggered",
            "Emergency alert raised — all escalation levels notified at once");
        AddLog(LogSeverity.Warning, "DataQuality", "rules-engine", "InsufficientData",
            "Participant flagged not_enough_information — Form 1224 overdue (>90d)");
    }

    private void SeedParticipants()
    {
        // clientId, first, last, prog, serviceLine, site, caseManager, hosp, selfMeds, reliability,
        // assessmentDetail, dangerSev, pDanger, pRefusal, pNoncomp, refusalStreak, alertClass, alertState, delta
        var seed = new (int id, string fn, string ln, string prog, ServiceLine sl, string? site, string? cm, bool hosp, bool self,
            DataReliability rel, string detail, double dsev, double pd, double pr, double pn, int streak, AlertClass ac, AlertState st, double delta)[]
        {
            (10481,"Eleanor","Hughes","BH RES",ServiceLine.Residential,"Oak House","Priya Nair",false,false,DataReliability.Sufficient,"Current",4.4,0.86,0.40,0.30,8,AlertClass.Emergency,AlertState.Open,+6),         // emergency behaviour
            (11290,"Marcus","Bennett","BH RES",ServiceLine.Residential,"Oak House","Sam Osei",false,true,DataReliability.Sufficient,"Current",3.1,0.44,0.82,0.74,21,AlertClass.Emergency,AlertState.Open,+5),            // emergency medication (clozapine restart)
            (20744,"Sofia","Ramirez","ICMS",ServiceLine.CaseManagement,null,null,false,true,DataReliability.Sufficient,"Current",2.4,0.52,0.38,0.33,4,AlertClass.Warning,AlertState.Open,-4),                             // no case manager
            (33817,"James","O'Connor","ICMS",ServiceLine.CaseManagement,null,"Daniel Kerr",false,true,DataReliability.NotEnoughInformation,"Form 1224 Overdue (>90d)",2.0,0.35,0.30,0.28,0,AlertClass.Warning,AlertState.Open,+1), // insufficient data
            (40912,"Aisha","Khan","SH SUBONLY",ServiceLine.SupportiveHousing,null,"Hannah Reed",false,true,DataReliability.Sufficient,"Current",2.2,0.30,0.88,0.80,164,AlertClass.Warning,AlertState.InProgress,+3),        // long refusal streak 150+
            (50233,"Robert","Feldman","BH RES",ServiceLine.Residential,"Birch House","Priya Nair",true,false,DataReliability.Sufficient,"Current",3.8,0.70,0.55,0.48,12,AlertClass.Warning,AlertState.OnHold,+1),           // hospitalised -> on hold
            (60187,"Grace","Lin","SH",ServiceLine.SupportiveHousing,null,"Sam Osei",false,true,DataReliability.Sufficient,"Current",1.6,0.22,0.18,0.20,2,AlertClass.Warning,AlertState.ReadyToClose,-18),                   // risk dropped below alert
            (70655,"Daniel","Osei","BH RES",ServiceLine.Residential,"Birch House","Daniel Kerr",false,false,DataReliability.Sufficient,"Current",4.1,0.78,0.42,0.36,6,AlertClass.Warning,AlertState.Open,+9),               // held high by recent 90d event
            (80441,"Priya","Sharma","ICMS",ServiceLine.CaseManagement,null,"Grace Lin",false,true,DataReliability.Sufficient,"Current",2.6,0.58,0.63,0.70,30,AlertClass.Warning,AlertState.InProgress,+4),                  // new event on open alert
            (90328,"Thomas","Wright","BH RES",ServiceLine.Residential,"Cedar House","Priya Nair",false,false,DataReliability.Sufficient,"Current",4.7,0.91,0.60,0.52,15,AlertClass.Emergency,AlertState.Acknowledged,+11),
            (10559,"Isabella","Rossi","SH",ServiceLine.SupportiveHousing,null,"Hannah Reed",false,true,DataReliability.Sufficient,"Current",1.4,0.18,0.24,0.22,1,AlertClass.Warning,AlertState.Open,-3),
            (21876,"William","Chen","ICMS",ServiceLine.CaseManagement,null,"Daniel Kerr",false,true,DataReliability.Sufficient,"Current",2.9,0.49,0.57,0.44,9,AlertClass.Warning,AlertState.Open,+2),
            (32094,"Olivia","Nguyen","SH SUBONLY",ServiceLine.SupportiveHousing,null,null,false,true,DataReliability.Sufficient,"Current",2.1,0.33,0.71,0.66,47,AlertClass.Warning,AlertState.Open,-5),                       // no case manager (second)
            (43310,"Henry","Adams","BH RES",ServiceLine.Residential,"Cedar House","Grace Lin",false,false,DataReliability.Sufficient,"Current",3.6,0.69,0.47,0.40,7,AlertClass.Warning,AlertState.Open,+3),
            (55401,"Margaret","Doyle","BH RES",ServiceLine.Residential,"Oak House","Daniel Kerr",false,true,DataReliability.Sufficient,"Current",3.3,0.64,0.52,0.58,11,AlertClass.Warning,AlertState.Acknowledged,+7),
            (66120,"Arthur","Bell","ICMS",ServiceLine.CaseManagement,null,"Alex Monroe",false,true,DataReliability.NotEnoughInformation,"Assessment overdue (>90d)",1.9,0.31,0.33,0.29,3,AlertClass.Warning,AlertState.Open,-1), // insufficient data (2)
            (77881,"Florence","Reid","SH",ServiceLine.SupportiveHousing,null,"Hannah Reed",false,true,DataReliability.Sufficient,"Current",2.3,0.40,0.44,0.39,5,AlertClass.Warning,AlertState.Open,+2),
            (88902,"George","Patel","SH SUBONLY",ServiceLine.SupportiveHousing,null,"Alex Monroe",false,true,DataReliability.Sufficient,"Current",2.0,0.28,0.59,0.75,152,AlertClass.Warning,AlertState.InProgress,+4),         // long refusal streak 150+ (2)
            (91010,"Nora","Flynn","BH RES",ServiceLine.Residential,"Birch House","Priya Nair",false,false,DataReliability.Sufficient,"Current",3.0,0.55,0.35,0.31,6,AlertClass.Warning,AlertState.Open,-2),
            (92220,"Victor","Hale","ICMS",ServiceLine.CaseManagement,null,"Sam Osei",false,true,DataReliability.Sufficient,"Current",2.7,0.47,0.49,0.52,14,AlertClass.Warning,AlertState.Open,+1),
            (93330,"Ruby","Carter","SH",ServiceLine.SupportiveHousing,null,"Grace Lin",false,true,DataReliability.Sufficient,"Current",1.7,0.24,0.27,0.25,2,AlertClass.Warning,AlertState.Open,-4),
            (94440,"Leo","Martins","BH RES",ServiceLine.Residential,"Cedar House","Daniel Kerr",true,false,DataReliability.Sufficient,"Current",3.9,0.73,0.50,0.46,10,AlertClass.Warning,AlertState.OnHold,+2),                 // hospitalised (2)
            (95550,"Ivy","Turner","SH SUBONLY",ServiceLine.SupportiveHousing,null,"Hannah Reed",false,true,DataReliability.Sufficient,"Current",2.5,0.36,0.68,0.72,58,AlertClass.Warning,AlertState.Open,+3),
            (96660,"Oscar","Grant","ICMS",ServiceLine.CaseManagement,null,"Alex Monroe",false,true,DataReliability.Sufficient,"Current",2.8,0.51,0.41,0.38,8,AlertClass.Warning,AlertState.Open,0),
            (97770,"Hazel","Ford","BH RES",ServiceLine.Residential,"Oak House","Grace Lin",false,false,DataReliability.Sufficient,"Current",4.0,0.80,0.44,0.35,9,AlertClass.Emergency,AlertState.Open,+8),
            (98880,"Felix","Ward","SH",ServiceLine.SupportiveHousing,null,"Sam Osei",false,true,DataReliability.Sufficient,"Current",1.5,0.20,0.22,0.19,1,AlertClass.Warning,AlertState.ReadyToClose,-12),                     // risk dropped (2)
            (99990,"Clara","Bishop","ICMS",ServiceLine.CaseManagement,null,"Daniel Kerr",false,true,DataReliability.Sufficient,"Current",2.6,0.45,0.62,0.55,26,AlertClass.Warning,AlertState.InProgress,+5),                  // new event on open alert (2)
            (10120,"Edwin","Shaw","BH RES",ServiceLine.Residential,"Birch House","Priya Nair",false,false,DataReliability.Sufficient,"Current",3.4,0.66,0.48,0.42,7,AlertClass.Warning,AlertState.Open,+3),
        };

        foreach (var s in seed)
        {
            var overall = Math.Round(Math.Max(s.pd, Math.Max(s.pr, s.pn)) * 100, 0);
            var p = new Participant
            {
                ClientId = s.id,
                PredictionId = $"PRD-{s.id}-{Runs[0].RunId}",
                FirstName = s.fn,
                LastName = s.ln,
                ProgrammeCode = s.prog,
                ServiceLine = s.sl,
                Site = s.site,
                CaseManager = s.cm,
                Hospitalised = s.hosp,
                SelfAdministeredMeds = s.self,
                DataReliability = s.rel,
                AssessmentStatusDetail = s.detail,
                DangerSeverity = s.dsev,
                CurrentRefusalStreak = s.streak,
                DaysSinceLastRefusal = s.streak > 0 ? 0 : Random.Shared.Next(3, 60),
                Noncompliance14d = s.pn > 0.5,
                DecliningAdherence = s.pn > 0.6,
                RiskScores = ScoreUtil.BuildScores(s.pd, s.pr, s.pn, s.dsev,
                    Math.Round(1 + s.pr * 4, 1), Math.Round(1 + s.pn * 4, 1)),
                OverallScore = overall,
                OverallBand = ScoreUtil.BandFromScore(overall),
                AlertClass = s.ac,
                AlertState = s.st,
                Delta = s.delta,
                LastAssessment = DateTime.UtcNow.AddHours(-Random.Shared.Next(2, 30))
            };
            Participants.Add(p);

            AddLog(LogSeverity.Audit, "ML.Fabric", "fabric@pipeline", "ScoreGenerated",
                $"Risk score {overall:0} ({p.OverallBand}) for {p.FullName} · {p.ProgrammeCode}", p.Id);
        }
    }
}
