using System.Net;
using System.Text;
using WorkcellOperatorConsole.Core.Services;

namespace WorkcellOperatorConsole.Tests;

public sealed class HttpWorkcellApiClientTests
{
    [Fact]
    public async Task StartVisionCycleUsesOnlyRestEndpoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK, CommandJson("Running"));
        var client = CreateClient(handler);

        var status = await client.StartCycleAsync("good-part");

        Assert.Equal("/api/machine/cycle", handler.Request?.RequestUri?.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Contains("\"sampleId\":\"good-part\"", handler.Body);
        Assert.Equal(WorkcellOperatorConsole.Core.Models.MachineState.Running, status.State);
    }

    [Fact]
    public async Task ConflictBecomesOperatorMeaningfulException()
    {
        var handler = new StubHandler(HttpStatusCode.Conflict, CommandJson("Offline", false));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<MachineCommandRejectedException>(
            () => client.SendCommandAsync("start")
        );

        Assert.Equal(WorkcellOperatorConsole.Core.Models.MachineState.Offline, exception.Status.State);
    }

    [Fact]
    public async Task HistoryRequestUsesPagedApi()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            "{\"items\":[],\"page\":2,\"pageSize\":25,\"totalCount\":0}"
        );
        var client = CreateClient(handler);

        await client.GetCyclesAsync(2, 25);

        Assert.Equal(
            "/api/cycles?page=2&pageSize=25",
            handler.Request?.RequestUri?.PathAndQuery
        );
    }

    private static HttpWorkcellApiClient CreateClient(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }
    );

    private static string CommandJson(string state, bool success = true) => $$"""
        {
          "success": {{success.ToString().ToLowerInvariant()}},
          "status": {
            "state": "{{state}}",
            "emergencyStopActive": false,
            "activeFault": null,
            "cycle": { "state": "WaitingForPart", "total": 0, "accepted": 0, "rejected": 0 },
            "robot": { "position": "Home", "moving": false, "initialized": true },
            "conveyor": { "running": false },
            "gripper": { "open": true },
            "partSensor": { "active": false }
          }
        }
        """;

    private sealed class StubHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }
}
