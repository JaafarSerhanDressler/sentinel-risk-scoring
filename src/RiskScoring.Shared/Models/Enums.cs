namespace RiskScoring.Shared.Models;

/// <summary>The three risk types scored by the Fabric pipeline.</summary>
public enum RiskType
{
    DangerousBehaviour,
    MedRefusal,
    MedNoncompliance
}

/// <summary>Risk bands (Power BI cut-offs).</summary>
public enum RiskBand
{
    Low,
    Moderate,
    High
}

public enum AlertClass
{
    Warning,
    Emergency
}

public enum AlertState
{
    Open,
    Acknowledged,
    InProgress,
    ReadyToClose,
    OnHold,
    Closed
}

public enum ServiceLine
{
    Residential,
    CaseManagement,
    SupportiveHousing,
    Day
}

public enum EventSource
{
    Dropdown,
    Nlp
}

public enum ClientRole
{
    Subject,
    Victim
}

public enum MissReason
{
    Refused,
    NotSupplied,
    Other
}

public enum DataReliability
{
    Sufficient,
    NotEnoughInformation
}

public enum RunStatus
{
    Running,
    Completed,
    Failed,
    Late
}

public enum AssessmentStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

/// <summary>Upstream systems feeding the Fabric / app (Credible, ChartMeds, …).</summary>
public enum DataSourceType
{
    Credible,
    ChartMeds,
    Fabric,
    Dayforce,
    Entra,
    Notifications
}

public enum DataSourceStatus
{
    Connected,
    Degraded,
    Offline
}

public enum LogSeverity
{
    Info,
    Success,
    Warning,
    Error,
    Audit
}
