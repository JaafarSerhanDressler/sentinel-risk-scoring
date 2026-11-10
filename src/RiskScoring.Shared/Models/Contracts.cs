namespace RiskScoring.Shared.Models;

public class ScoreRequest
{
    public string ParticipantId { get; set; } = "";
    public List<string> DataSourceKeys { get; set; } = new();
    public string RequestedBy { get; set; } = "Alex Monroe";
}

public class ScoreResponse
{
    public AssessmentRun Run { get; set; } = new();
    public Participant Participant { get; set; } = new();
    public List<RiskEvent> RiskEvents { get; set; } = new();
    public List<MedicationEvent> MedicationEvents { get; set; } = new();
}

public class ParticipantDetail
{
    public Participant Participant { get; set; } = new();
    public List<RiskEvent> RiskEvents { get; set; } = new();
    public List<MedicationEvent> MedicationEvents { get; set; } = new();
    public List<RiskTrendPoint> Trend { get; set; } = new();
}

public class ProgrammeDetail
{
    public Programme Programme { get; set; } = new();
    public List<Participant> Participants { get; set; } = new();
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthUser
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";     // Frontline, Supervisor, Clinical, Administrator
    public string Email { get; set; } = "";
    public string Initials { get; set; } = "";
    public string Token { get; set; } = "";
}
