using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ModelOps.Application;

namespace ModelOps.Desktop;

public sealed class ModelOpsApiClient
{
    private readonly HttpClient _httpClient = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public void SetBaseAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("API address is invalid.");
        }

        _httpClient.BaseAddress = uri;
    }

    public async Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<List<ProjectResponse>>(
                   "api/v1/projects",
                   _jsonOptions,
                   cancellationToken)
               ?? [];
    }

    public async Task<ProjectResponse> CreateProjectAsync(
        string name,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/projects",
            new CreateProjectRequest(name),
            _jsonOptions,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(_jsonOptions, cancellationToken))!;
    }

    public async Task GenerateElementsAsync(
        Guid projectId,
        int count,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/projects/{projectId}/generate",
            new GenerateElementsRequest(count),
            _jsonOptions,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ValidationRunResponse> StartCheckAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"api/v1/projects/{projectId}/checks",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ValidationRunResponse>(_jsonOptions, cancellationToken))!;
    }

    public async Task<ValidationRunResponse> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        return (await _httpClient.GetFromJsonAsync<ValidationRunResponse>(
            $"api/v1/checks/{runId}",
            _jsonOptions,
            cancellationToken))!;
    }

    public async Task<PagedResponse<IssueResponse>> GetIssuesAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        return (await _httpClient.GetFromJsonAsync<PagedResponse<IssueResponse>>(
            $"api/v1/checks/{runId}/issues?pageSize=200",
            _jsonOptions,
            cancellationToken))!;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"API returned {(int)response.StatusCode} {response.ReasonPhrase}: {details}");
    }
}
