#include "machine/Command.hpp"

#include <algorithm>
#include <cctype>
#include <sstream>

namespace workcell {

Command parseCommand(const std::string& input)
{
    std::istringstream stream(input);

    std::string commandText;
    stream >> commandText;

    std::transform(
        commandText.begin(),
        commandText.end(),
        commandText.begin(),
        [](unsigned char character)
        {
            return static_cast<char>(
                std::tolower(character)
            );
        }
    );

    std::string argument;
    std::getline(stream, argument);

    if (!argument.empty() && argument.front() == ' ')
    {
        argument.erase(0, 1);
    }

    if (commandText == "status")
    {
        return {CommandType::Status, argument};
    }

    if (commandText == "initialize")
    {
        return {CommandType::Initialize, argument};
    }

    if (commandText == "start")
    {
        return {CommandType::Start, argument};
    }

    if (commandText == "pause")
    {
        return {CommandType::Pause, argument};
    }

    if (commandText == "resume")
    {
        return {CommandType::Resume, argument};
    }

    if (commandText == "stop")
    {
        return {CommandType::Stop, argument};
    }

    if (commandText == "reset")
    {
        return {CommandType::Reset, argument};
    }

    if (commandText == "estop")
    {
        return {CommandType::EmergencyStop, argument};
    }

    if (commandText == "clear-estop")
    {
        return {CommandType::ClearEmergencyStop, argument};
    }

    if (commandText == "fault")
    {
        return {CommandType::InjectFault, argument};
    }

    if (commandText == "help")
    {
        return {CommandType::Help, argument};
    }

    if (commandText == "exit")
    {
        return {CommandType::Exit, argument};
    }

    return {CommandType::Invalid, argument};
}

}