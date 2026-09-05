using MachineService.Models;

namespace MachineService.Transport;

internal sealed record IpcRequest(string RequestId, string Command, object Payload);
internal sealed record IpcError(string Code, string Message);
internal sealed record IpcResponse(
    string RequestId,
    bool Success,
    ControllerResponse? Status,
    IpcError? Error
);
