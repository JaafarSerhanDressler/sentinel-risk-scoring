using RiskScoring.Shared.Models;

namespace RiskScoring.Client.Services;

public static class Ui
{
    // ----- Risk bands --------------------------------------------------
    public static string BandClass(RiskBand b) => b switch
    {
        RiskBand.Low => "low",
        RiskBand.Moderate => "moderate",
        _ => "critical"          // High = red (reuse critical styling)
    };

    public static string BandColor(RiskBand b) => b switch
    {
        RiskBand.Low => "#10b981",
        RiskBand.Moderate => "#f59e0b",
        _ => "#ef4444"
    };

    public static string BandLabel(RiskBand b) => b switch
    {
        RiskBand.Low => "Low",
        RiskBand.Moderate => "Moderate",
        _ => "High"
    };

    // ----- Alerts ------------------------------------------------------
    public static string AlertClassLabel(AlertClass c) => c == AlertClass.Emergency ? "Emergency" : "Warning";
    public static string AlertClassColor(AlertClass c) => c == AlertClass.Emergency ? "#ef4444" : "#f59e0b";

    public static string AlertStateLabel(AlertState s) => s switch
    {
        AlertState.Open => "Open",
        AlertState.Acknowledged => "Acknowledged",
        AlertState.InProgress => "In progress",
        AlertState.ReadyToClose => "Ready to close",
        AlertState.OnHold => "On hold",
        _ => "Closed"
    };

    public static string AlertStateColor(AlertState s) => s switch
    {
        AlertState.Open => "#38bdf8",
        AlertState.Acknowledged => "#22d3ee",
        AlertState.InProgress => "#6366f1",
        AlertState.ReadyToClose => "#10b981",
        AlertState.OnHold => "#f59e0b",
        _ => "#64748b"
    };

    // ----- Risk types --------------------------------------------------
    public static string RiskTypeLabel(RiskType t) => t switch
    {
        RiskType.DangerousBehaviour => "Dangerous behaviour",
        RiskType.MedRefusal => "Medication refusal",
        _ => "Medication non-compliance"
    };

    public static string RiskTypeIcon(RiskType t) => t switch
    {
        RiskType.DangerousBehaviour => "alert",
        RiskType.MedRefusal => "pill",
        _ => "clock"
    };

    // ----- Data sources ------------------------------------------------
    public static string SourceIcon(DataSourceType t) => t switch
    {
        DataSourceType.Credible => "database",
        DataSourceType.ChartMeds => "pill",
        DataSourceType.Fabric => "cpu",
        DataSourceType.Dayforce => "users",
        DataSourceType.Entra => "shield",
        DataSourceType.Notifications => "bell",
        _ => "layers"
    };

    public static string StatusClass(DataSourceStatus s) => s switch
    {
        DataSourceStatus.Connected => "st-connected",
        DataSourceStatus.Degraded => "st-degraded",
        _ => "st-offline"
    };

    public static string RunStatusColor(RunStatus s) => s switch
    {
        RunStatus.Completed => "#10b981",
        RunStatus.Running => "#38bdf8",
        RunStatus.Late => "#f59e0b",
        _ => "#ef4444"
    };

    public static string ServiceLineLabel(ServiceLine s) => s switch
    {
        ServiceLine.Residential => "Residential",
        ServiceLine.CaseManagement => "Case management",
        ServiceLine.SupportiveHousing => "Supportive housing",
        _ => "Day"
    };

    public static string LogClass(LogSeverity s) => s.ToString().ToLowerInvariant();

    public static string TimeAgo(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
