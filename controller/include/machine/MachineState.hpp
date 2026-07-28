#pragma once

#include <string>

namespace workcell {

enum class MachineState
{
    Offline,
    Initializing,
    Idle,
    Running,
    Paused,
    Faulted,
    EmergencyStop,
    Stopping
};

std::string toString(MachineState state);

}