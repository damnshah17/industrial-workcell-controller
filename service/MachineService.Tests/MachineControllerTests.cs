using MachineService.Models;
using MachineService.Services;
using Microsoft.AspNetCore.Mvc;
using ApiController = MachineService.Controllers.MachineController;

namespace MachineService.Tests;

public sealed class MachineControllerTests
{
    [Fact]
    public async Task CycleEndpointUsesSampleInsteadOfCallerDecision()
    {
        var machine = new RecordingMachineService();
        var controller = new ApiController(machine);

        var result = await controller.StartCycle(
            new StartCycleRequest("good-part"),
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("good-part", machine.SampleId);
        Assert.Null(machine.ManualDecision);
    }

    private sealed class RecordingMachineService : IMachineService
    {
        public string? SampleId { get; private set; }
        public bool? ManualDecision { get; private set; }

        public Task<MachineStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Status());
        public Task<bool> StartCycleAsync(string sampleId, CancellationToken cancellationToken = default)
        {
            SampleId = sampleId;
            return Task.FromResult(true);
        }
        public Task<bool> StartCycleAsync(bool inspectionAccepted, CancellationToken cancellationToken = default)
        {
            ManualDecision = inspectionAccepted;
            return Task.FromResult(true);
        }
        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> PauseAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> ResumeAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> StopAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> ResetAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> EmergencyStopAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> ClearEmergencyStopAsync(CancellationToken cancellationToken = default) => Accepted();
        public Task<bool> InjectMotionTimeoutFaultAsync(CancellationToken cancellationToken = default) => Accepted();

        private static Task<bool> Accepted() => Task.FromResult(true);
        private static MachineStatus Status() => new(
            MachineState.Running, false, null,
            new CycleStatus("StoppingConveyor", 0, 0, 0),
            new RobotStatus("Home", false, true),
            new ConveyorStatus(false), new GripperStatus(true),
            new PartSensorStatus(true)
        );
    }
}
