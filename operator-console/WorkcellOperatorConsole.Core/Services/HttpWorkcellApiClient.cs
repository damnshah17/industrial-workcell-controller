using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkcellOperatorConsole.Core.Models;

namespace WorkcellOperatorConsole.Core.Services;

public sealed class HttpWorkcellApiClient : IWorkcellApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpWorkcellApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<MachineStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        GetAsync<MachineStatus>("api/machine/status", cancellationToken);

    public Task<MachineStatus> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default
    ) => PostAsync($"api/machine/{command}", null, cancellationToken);

    public Task<MachineStatus> StartCycleAsync(
        string sampleId,
        CancellationToken cancellationToken = default
    ) => PostAsync(
        "api/machine/cycle",
        new { sampleId },
        cancellationToken
    );

    public Task<PagedResult<MachineEvent>> GetEventsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<MachineEvent>>($"api/events?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<ProductionCycle>> GetCyclesAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<ProductionCycle>>($"api/cycles?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<FaultEvent>> GetFaultsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<FaultEvent>>($"api/faults?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<ProductionMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ProductionMetrics>("api/metrics", cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<MachineStatus> PostAsync(
        string path,
        object? body,
        CancellationToken cancellationToken
    )
    {
        using var response = body is null
            ? await _httpClient.PostAsync(path, null, cancellationToken)
            : await _httpClient.PostAsJsonAsync(path, body, _jsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var rejected = await ReadAsync<CommandResponse>(response, cancellationToken);
            throw new MachineCommandRejectedException(rejected.Status);
        }

        response.EnsureSuccessStatusCode();
        return (await ReadAsync<CommandResponse>(response, cancellationToken)).Status;
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
        ?? throw new InvalidOperationException("Machine service returned an empty response.");
}
