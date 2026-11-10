using System.Net.Http.Json;
using RiskScoring.Shared.Models;

namespace RiskScoring.Client.Services;

/// <summary>Typed gateway to the Sentinel API (which fronts the ML Fabric).</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public Task<List<Participant>?> GetParticipantsAsync() =>
        _http.GetFromJsonAsync<List<Participant>>("api/participants");

    public Task<ParticipantDetail?> GetParticipantAsync(string id) =>
        _http.GetFromJsonAsync<ParticipantDetail>($"api/participants/{id}");

    public Task<List<Programme>?> GetProgrammesAsync() =>
        _http.GetFromJsonAsync<List<Programme>>("api/programmes");

    public Task<ProgrammeDetail?> GetProgrammeAsync(string code) =>
        _http.GetFromJsonAsync<ProgrammeDetail>($"api/programmes/{Uri.EscapeDataString(code)}");

    public Task<List<FabricRun>?> GetRunsAsync(int take = 8) =>
        _http.GetFromJsonAsync<List<FabricRun>>($"api/runs?take={take}");

    public Task<List<DataSource>?> GetDataSourcesAsync() =>
        _http.GetFromJsonAsync<List<DataSource>>("api/datasources");

    public Task<KpiSummary?> GetKpisAsync() =>
        _http.GetFromJsonAsync<KpiSummary>("api/dashboard/kpis");

    public Task<List<RiskDistributionBucket>?> GetRiskDistributionAsync() =>
        _http.GetFromJsonAsync<List<RiskDistributionBucket>>("api/dashboard/risk-distribution");

    public Task<List<ThroughputPoint>?> GetThroughputAsync() =>
        _http.GetFromJsonAsync<List<ThroughputPoint>>("api/dashboard/throughput");

    public Task<ModelMetrics?> GetMetricsAsync() =>
        _http.GetFromJsonAsync<ModelMetrics>("api/admin/metrics");

    public Task<List<AssessmentRun>?> GetAssessmentsAsync(int take = 8) =>
        _http.GetFromJsonAsync<List<AssessmentRun>>($"api/assessments?take={take}");

    public Task<List<LogEntry>?> GetLogsAsync(string? level = null, int take = 60) =>
        _http.GetFromJsonAsync<List<LogEntry>>($"api/logs?take={take}" + (level is null ? "" : $"&level={level}"));

    public async Task<ScoreResponse?> RunAssessmentAsync(ScoreRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/assessments", request);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<ScoreResponse>() : null;
    }

    public async Task<AuthUser?> LoginAsync(LoginRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login", request);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthUser>() : null;
    }
}
