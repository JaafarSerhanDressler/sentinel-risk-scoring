using RiskScoring.Shared.Models;

namespace RiskScoring.Api.Services;

/// <summary>
/// Mock of the external ML Fabric. On an app-requested re-score it simulates
/// feature assembly from the selected data sources, inference latency, scoring
/// of the three risk types, event attribution, and writes the audit log.
/// </summary>
public class MlFabricService
{
    private readonly DataStore _store;
    public MlFabricService(DataStore store) => _store = store;

    public async Task<ScoreResponse?> ScoreAsync(ScoreRequest request)
    {
        var p = _store.Participants.FirstOrDefault(x => x.Id == request.ParticipantId);
        if (p is null) return null;

        var correlation = Guid.NewGuid().ToString("N")[..8];
        var sources = request.DataSourceKeys.Count > 0
            ? request.DataSourceKeys
            : new List<string> { "credible", "chartmeds", "fabric" };

        var run = new AssessmentRun
        {
            ParticipantId = p.Id,
            ParticipantName = p.FullName,
            RequestedBy = request.RequestedBy,
            StartedAt = DateTime.UtcNow,
            Status = AssessmentStatus.Running
        };

        _store.AddLog(LogSeverity.Info, "ML.Fabric", request.RequestedBy, "AssessmentRequested",
            $"Re-score queued for {p.FullName} using {sources.Count} source(s)", p.Id, correlation);

        await Task.Delay(Random.Shared.Next(650, 1100));

        var coverage = EvaluateCoverage(sources);
        var prev = p.OverallScore;

        // Nudge each risk type a little; keep within band logic.
        double Nudge(double v) => Math.Clamp(v + (Random.Shared.NextDouble() - 0.45) * 0.12, 0.02, 0.98);
        var pd = Nudge(p.RiskScores.First(r => r.Type == RiskType.DangerousBehaviour).Probability);
        var pr = Nudge(p.RiskScores.First(r => r.Type == RiskType.MedRefusal).Probability);
        var pn = Nudge(p.RiskScores.First(r => r.Type == RiskType.MedNoncompliance).Probability);

        p.RiskScores = ScoreUtil.BuildScores(pd, pr, pn, p.DangerSeverity,
            Math.Round(1 + pr * 4, 1), Math.Round(1 + pn * 4, 1));
        p.OverallScore = Math.Round(Math.Max(pd, Math.Max(pr, pn)) * 100, 0);
        p.OverallBand = ScoreUtil.BandFromScore(p.OverallScore);
        p.Delta = Math.Round(p.OverallScore - prev, 0);
        p.LastAssessment = DateTime.UtcNow;
        p.PredictionId = $"PRD-{p.ClientId}-{DateTime.UtcNow:yyyyMMddHHmm}";

        var (events, meds) = ScoreUtil.EventsFor(p);

        run.Status = AssessmentStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        run.DurationMs = (int)(run.CompletedAt.Value - run.StartedAt).TotalMilliseconds + Random.Shared.Next(700, 1400);
        run.OverallScore = p.OverallScore;
        run.OverallBand = p.OverallBand;
        run.AlertClass = p.AlertClass;

        _store.AddAssessment(run);
        _store.RecomputeProgrammes();
        _store.AddLog(LogSeverity.Audit, "ML.Fabric", request.RequestedBy, "ScoreGenerated",
            $"Risk score {p.OverallScore:0} ({p.OverallBand}) for {p.FullName} · confidence {coverage:P0}", p.Id, correlation);

        if (p.AlertClass == AlertClass.Emergency)
            _store.AddLog(LogSeverity.Warning, "CareOps", "rules-engine", "EscalationTriggered",
                $"Emergency alert for {p.FullName} — all escalation levels notified", p.Id, correlation);

        return new ScoreResponse { Run = run, Participant = p, RiskEvents = events, MedicationEvents = meds };
    }

    private double EvaluateCoverage(List<string> keys)
    {
        var selected = _store.DataSources.Where(d => keys.Contains(d.Key)).ToList();
        if (selected.Count == 0) return 0.6;
        return Math.Clamp(0.55 + selected.Average(d => d.Quality) * 0.4, 0.5, 0.98);
    }

    public List<RiskTrendPoint> TrendFor(Participant p)
    {
        var points = new List<RiskTrendPoint>();
        var rng = new Random(p.ClientId);
        var current = p.OverallScore;
        var value = Math.Clamp(current - rng.Next(8, 22), 5, 99);
        for (int i = 11; i >= 0; i--)
        {
            value = Math.Clamp(value + (rng.NextDouble() - 0.42) * 9, 5, 99);
            if (i == 0) value = current;
            points.Add(new RiskTrendPoint { Date = DateTime.UtcNow.AddDays(-i * 7), Score = Math.Round(value, 0) });
        }
        return points;
    }
}
