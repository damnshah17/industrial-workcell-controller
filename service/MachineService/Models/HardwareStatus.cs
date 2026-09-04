namespace MachineService.Models;

public sealed record RobotStatus(
    string Position,
    bool Moving,
    bool Initialized
);

public sealed record ConveyorStatus(
    bool Running
);

public sealed record GripperStatus(
    bool Open
);

public sealed record PartSensorStatus(
    bool Active
);
