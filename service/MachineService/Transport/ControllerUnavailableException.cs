namespace MachineService.Transport;

public sealed class ControllerUnavailableException : Exception
{
    public ControllerUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
