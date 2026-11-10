namespace RiskScoring.Shared.Models;

/// <summary>A participant (client) scored by the Fabric risk pipeline.</summary>
public class Participant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public int ClientId { get; set; }
    public string PredictionId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ProgrammeCode { get; set; } = "";       // BH RES, ICMS, SH, SH SUBONLY
    public ServiceLine ServiceLine { get; set; }
    public string? Site { get; set; }                     // null until a site source exists
    public string? CaseManager { get; set; }              // null = no case manager

    public bool Hospitalised { get; set; }
    public bool SelfAdministeredMeds { get; set; }
    public DataReliability DataReliability { get; set; }
    public string AssessmentStatusDetail { get; set; } = "";

    public double DangerSeverity { get; set; }            // 1 - 5
    public int CurrentRefusalStreak { get; set; }
    public int DaysSinceLastRefusal { get; set; }
    public bool Noncompliance14d { get; set; }
    public bool DecliningAdherence { get; set; }

    public List<RiskTypeScore> RiskScores { get; set; } = new();

    public double OverallScore { get; set; }              // 0 - 100 (gauge)
    public RiskBand OverallBand { get; set; }
    public AlertClass AlertClass { get; set; }
    public AlertState AlertState { get; set; }
    public double Delta { get; set; }
    public DateTime LastAssessment { get; set; }

    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}".Trim().ToUpperInvariant();
    public RiskTypeScore? TopRisk => RiskScores.OrderByDescending(r => r.Probability).FirstOrDefault();
}

public class RiskTypeScore
{
    public RiskType Type { get; set; }
    public double Probability { get; set; }   // 0 - 1
    public double Severity { get; set; }       // 1 - 5
    public RiskBand Band { get; set; }
}

/// <summary>A behaviour event in the last 90 days.</summary>
public class RiskEvent
{
    public string EventId { get; set; } = "";
    public DateTime Date { get; set; }
    public string Category { get; set; } = "";
    public string? Subtype { get; set; }
    public EventSource Source { get; set; }
    public AlertClass EventClass { get; set; }
    public int Severity { get; set; }            // 1 - 5
    public ClientRole ClientRole { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>A missed medication dose in the last 90 days.</summary>
public class MedicationEvent
{
    public string MedEventId { get; set; } = "";
    public DateTime Date { get; set; }
    public string MedicationName { get; set; } = "";
    public string MedicationClass { get; set; } = "";
    public int Severity { get; set; }            // 1 - 5
    public AlertClass EventClass { get; set; }
    public MissReason MissReason { get; set; }
    public bool IsPrn { get; set; }
    public string? RestartFlag { get; set; }     // clozapine_2d, exelon_3d, lamictal_5d
}

public class RiskTrendPoint
{
    public DateTime Date { get; set; }
    public double Score { get; set; }
}
