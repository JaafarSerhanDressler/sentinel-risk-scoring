using RiskScoring.Shared.Models;

namespace RiskScoring.Api.Services;

/// <summary>Risk banding, action guidance and synthetic event generation.</summary>
public static class ScoreUtil
{
    public static RiskBand BandFromScore(double score) => score switch
    {
        >= 70 => RiskBand.High,
        >= 40 => RiskBand.Moderate,
        _ => RiskBand.Low
    };

    public static RiskBand BandFromProb(double prob) => prob switch
    {
        >= 0.67 => RiskBand.High,
        >= 0.34 => RiskBand.Moderate,
        _ => RiskBand.Low
    };

    public static string RiskTypeLabel(RiskType t) => t switch
    {
        RiskType.DangerousBehaviour => "Dangerous behaviour",
        RiskType.MedRefusal => "Medication refusal",
        _ => "Medication non-compliance"
    };

    public static List<RiskTypeScore> BuildScores(double pDanger, double pRefusal, double pNoncomp,
        double sevDanger, double sevRefusal, double sevNoncomp)
    {
        return new()
        {
            new() { Type = RiskType.DangerousBehaviour, Probability = pDanger, Severity = sevDanger, Band = BandFromProb(pDanger) },
            new() { Type = RiskType.MedRefusal, Probability = pRefusal, Severity = sevRefusal, Band = BandFromProb(pRefusal) },
            new() { Type = RiskType.MedNoncompliance, Probability = pNoncomp, Severity = sevNoncomp, Band = BandFromProb(pNoncomp) },
        };
    }

    private static readonly (string cat, string? sub)[] BehaviourCats =
    {
        ("Physical aggression", null), ("Verbal threat", null), ("Self-harm", null),
        ("Elopement", null), ("Property damage", null), ("Contraband", "Substance"),
        ("Contraband", "Weapon"), ("Unusual incident", "Altercation"), ("Substance use", null)
    };

    private static readonly (string name, string cls, string? restart)[] Meds =
    {
        ("Clozapine", "Antipsychotic", "clozapine_2d"),
        ("Risperidone", "Antipsychotic", null),
        ("Olanzapine", "Antipsychotic", null),
        ("Lamotrigine", "Mood stabiliser", "lamictal_5d"),
        ("Lithium", "Mood stabiliser", null),
        ("Rivastigmine", "Cholinesterase", "exelon_3d"),
        ("Sertraline", "Antidepressant", null),
    };

    public static (List<RiskEvent> events, List<MedicationEvent> meds) EventsFor(Participant p)
    {
        var rng = new Random(p.ClientId);
        var events = new List<RiskEvent>();
        var meds = new List<MedicationEvent>();

        var danger = p.RiskScores.First(r => r.Type == RiskType.DangerousBehaviour);
        var behaviourCount = danger.Band == RiskBand.High ? rng.Next(2, 5) : danger.Band == RiskBand.Moderate ? rng.Next(1, 3) : rng.Next(0, 2);

        for (int i = 0; i < behaviourCount; i++)
        {
            var (cat, sub) = BehaviourCats[rng.Next(BehaviourCats.Length)];
            var emergency = p.AlertClass == AlertClass.Emergency && i == 0;
            events.Add(new RiskEvent
            {
                EventId = $"EV-{p.ClientId}-{i}",
                Date = DateTime.UtcNow.AddDays(-rng.Next(1, 88)),
                Category = cat,
                Subtype = sub,
                Source = rng.Next(2) == 0 ? EventSource.Dropdown : EventSource.Nlp,
                EventClass = emergency ? AlertClass.Emergency : AlertClass.Warning,
                Severity = emergency ? rng.Next(4, 6) : rng.Next(1, 4),
                ClientRole = rng.Next(5) == 0 ? ClientRole.Victim : ClientRole.Subject,
                Note = "Documented in Credible report; safe summary only."
            });
        }

        var refusal = p.RiskScores.First(r => r.Type == RiskType.MedRefusal);
        var noncomp = p.RiskScores.First(r => r.Type == RiskType.MedNoncompliance);
        var highMed = refusal.Band == RiskBand.High || noncomp.Band == RiskBand.High;
        var medCount = highMed ? rng.Next(2, 5) : rng.Next(0, 3);

        for (int i = 0; i < medCount; i++)
        {
            var (name, cls, restart) = Meds[rng.Next(Meds.Length)];
            var emergency = cls == "Antipsychotic" && restart is not null && rng.Next(3) == 0;
            meds.Add(new MedicationEvent
            {
                MedEventId = $"MED-{p.ClientId}-{i}",
                Date = DateTime.UtcNow.AddDays(-rng.Next(1, 45)),
                MedicationName = name,
                MedicationClass = cls,
                Severity = emergency ? rng.Next(4, 6) : rng.Next(1, 4),
                EventClass = emergency ? AlertClass.Emergency : AlertClass.Warning,
                MissReason = (MissReason)rng.Next(3),
                IsPrn = rng.Next(6) == 0,
                RestartFlag = emergency ? restart : null
            });
        }

        return (events.OrderByDescending(e => e.Date).ToList(), meds.OrderByDescending(m => m.Date).ToList());
    }

    /// <summary>Recommended response actions (the app's seven categories).</summary>
    public static List<(string title, string detail, string priority, string icon)> ActionsFor(Participant p)
    {
        var list = new List<(string, string, string, string)>();
        if (p.AlertClass == AlertClass.Emergency)
        {
            list.Add(("Immediate welfare check", "Lay eyes on the participant now and confirm safety.", "Urgent", "alert"));
            list.Add(("Notify on-call clinician", "Emergency alert — every escalation level is notified at once.", "Urgent", "phone"));
        }
        var topRefusal = p.RiskScores.Where(r => r.Type != RiskType.DangerousBehaviour).OrderByDescending(r => r.Probability).First();
        if (topRefusal.Band is RiskBand.High or RiskBand.Moderate)
            list.Add(("Medication follow-up", "Pharmacy/nurse review of refusals, non-compliance and restart flags.", "High", "pill"));
        if (p.RiskScores.First(r => r.Type == RiskType.DangerousBehaviour).Band is RiskBand.High or RiskBand.Moderate)
            list.Add(("De-escalation & safety plan", "Apply the behaviour support plan and document techniques used.", "High", "shield"));
        if (p.DataReliability == DataReliability.NotEnoughInformation)
            list.Add(("Complete overdue assessment", p.AssessmentStatusDetail, "Routine", "clipboard"));
        if (list.Count == 0)
            list.Add(("Document routine check", "Record a welfare check and keep the care plan current.", "Routine", "check"));
        return list;
    }
}
