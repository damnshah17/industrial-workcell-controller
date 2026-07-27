#include "machine/MachineState.hpp"

namespace workcell {

std::string toString(MachineState state)
{
    switch (state)
    {
        case MachineState::Offline:
            return "Offline";

        case MachineState::Initializing:
            return "Initializing";

        case MachineState::Idle:
            return "Idle";

        case MachineState::Running:
            return "Running";

        case MachineState::Paused:
            return "Paused";

        case MachineState::Faulted:
            return "Faulted";

        case MachineState::EmergencyStop:
            return "EmergencyStop";

        case MachineState::Stopping:
            return "Stopping";
    }

    return "Unknown";
}

}