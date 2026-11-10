namespace RiskScoring.Shared.Models;

/// <summary>A programme grouping (BH RES, ICMS, SH, SH SUBONLY).</summary>
public class Programme
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public ServiceLine ServiceLine { get; set; }
    public int ParticipantCount { get; set; }
    public int OpenAlerts { get; set; }
    public int Emergencies { get; set; }
    public int HighBandCount { get; set; }
    public double AverageScore { get; set; }
}

public class DataSource
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public DataSourceType Type { get; set; }
    public DataSourceStatus Status { get; set; }
    public DateTime LastSync { get; set; }
    public long RecordsIngested { get; set; }
    public int LatencyMs { get; set; }
    public double Quality { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>A daily Fabric ingestion run (run_status / ingest_run).</summary>
public class FabricRun
{
    public string RunId { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public RunStatus Status { get; set; }
    public int ParticipantRows { get; set; }
    public int EventRows { get; set; }
    public int MedicationEventRows { get; set; }
    public DateTime CredibleAsOf { get; set; }
    public DateTime ChartMedsAsOf { get; set; }
}

/// <summary>An on-demand re-score requested from the app.</summary>
public class AssessmentRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string ParticipantId { get; set; } = "";
    public string ParticipantName { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public AssessmentStatus Status { get; set; }
    public int DurationMs { get; set; }
    public double OverallScore { get; set; }
    public RiskBand OverallBand { get; set; }
    public AlertClass AlertClass { get; set; }
}

public class LogEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public LogSeverity Level { get; set; }
    public string Category { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ParticipantId { get; set; }
    public string Correlation { get; set; } = "";
}

public class ModelMetrics
{
    public string ModelVersion { get; set; } = "";
    public string Algorithm { get; set; } = "";
    public double Auc { get; set; }
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double Drift { get; set; }
    public DateTime LastTrained { get; set; }
    public long PredictionsToday { get; set; }
    public int AvgLatencyMs { get; set; }
    public double Uptime { get; set; }
    public DateTime CredibleAsOf { get; set; }
    public DateTime ChartMedsAsOf { get; set; }
}

public class KpiSummary
{
    public int TotalParticipants { get; set; }
    public int TotalProgrammes { get; set; }
    public int OpenAlerts { get; set; }
    public int Emergencies { get; set; }
    public int HighBandCount { get; set; }
    public int AssessmentsToday { get; set; }
    public double OpenAlertsTrend { get; set; }
    public double EmergenciesTrend { get; set; }
}

public class RiskDistributionBucket
{
    public RiskBand Band { get; set; }
    public int Count { get; set; }
}

public class ThroughputPoint
{
    public string Label { get; set; } = "";
    public int Assessments { get; set; }
    public int AvgLatencyMs { get; set; }
}
