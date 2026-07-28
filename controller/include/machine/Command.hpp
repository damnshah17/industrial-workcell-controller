#pragma once

#include <string>

namespace workcell {

enum class CommandType
{
    Status,
    Initialize,
    Start,
    Pause,
    Resume,
    Stop,
    Reset,
    EmergencyStop,
    ClearEmergencyStop,
    InjectFault,
    Help,
    Exit,
    Invalid
};

struct Command
{
    CommandType type;
    std::string argument;
};

Command parseCommand(const std::string& input);

}