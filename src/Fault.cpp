#include "faults/Fault.hpp"

namespace workcell {

std::string toString(FaultCode code)
{
    switch (code)
    {
        case FaultCode::InitializationFailure:
            return "INITIALIZATION_FAILURE";

        case FaultCode::RobotCommunicationLoss:
            return "ROBOT_COMMUNICATION_LOSS";

        case FaultCode::MotionTimeout:
            return "MOTION_TIMEOUT";

        case FaultCode::ConveyorFailure:
            return "CONVEYOR_FAILURE";

        case FaultCode::SensorFailure:
            return "SENSOR_FAILURE";

        case FaultCode::GripperFailure:
            return "GRIPPER_FAILURE";

        case FaultCode::InspectionFailure:
            return "INSPECTION_FAILURE";

        case FaultCode::SafetyDoorOpen:
            return "SAFETY_DOOR_OPEN";
    }

    return "UNKNOWN_FAULT";
}

}