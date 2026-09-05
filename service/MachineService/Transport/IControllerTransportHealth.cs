using MachineService.Reliability;

namespace MachineService.Transport;

public interface IControllerTransportHealth
{
    ControllerTransportHealth GetHealth();
}
