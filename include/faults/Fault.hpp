#pragma once

#include <string>

namespace workcell {

enum class FaultCode
{
    InitializationFailure,
    RobotCommunicationLoss,
    MotionTimeout,
    ConveyorFailure,
    SensorFailure,
    GripperFailure,
    InspectionFailure,
    SafetyDoorOpen
};

struct Fault
{
    FaultCode code;
    std::string message;
};

std::string toString(FaultCode code);

}