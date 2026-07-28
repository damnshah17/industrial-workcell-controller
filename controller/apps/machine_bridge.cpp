#include "faults/FaultManager.hpp"
#include "machine/MachineController.hpp"
#include "machine/MachineState.hpp"
#include "safety/SafetyController.hpp"
#include "sequence/SequenceController.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

#include <iostream>
#include <string>

namespace {

constexpr const char* RESPONSE_PREFIX =
    "@@RESPONSE@@";

std::string escapeJson(
    const std::string& value
)
{
    std::string result;

    for (char character : value)
    {
        switch (character)
        {
        case '"':
            result += "\\\"";
            break;

        case '\\':
            result += "\\\\";
            break;

        case '\n':
            result += "\\n";
            break;

        case '\r':
            result += "\\r";
            break;

        case '\t':
            result += "\\t";
            break;

        default:
            result += character;
            break;
        }
    }

    return result;
}

void sendResponse(
    bool success,
    const workcell::MachineController& machine,
    const workcell::SequenceController& sequence
)
{
    std::cout
        << RESPONSE_PREFIX
        << "{"
        << "\"success\":"
        << (success ? "true" : "false")
        << ","
        << "\"state\":\""
        << workcell::toString(machine.getState())
        << "\","
        << "\"emergencyStopActive\":"
        << (
            machine.isEmergencyStopActive()
                ? "true"
                : "false"
        )
        << ","
        << "\"hasActiveFault\":"
        << (
            machine.hasActiveFault()
                ? "true"
                : "false"
        );

    if (machine.hasActiveFault())
    {
        const auto& fault =
            machine.getActiveFault().value();

        std::cout
            << ","
            << "\"fault\":{"
            << "\"code\":\""
            << workcell::toString(fault.code)
            << "\","
            << "\"message\":\""
            << escapeJson(fault.message)
            << "\""
            << "}";
    }
    else
    {
        std::cout
            << ",\"fault\":null";
    }

    std::cout
        << ","
        << "\"cycle\":{"
        << "\"state\":\""
        << workcell::toString(
            sequence.getState()
        )
        << "\","
        << "\"total\":"
        << sequence.getTotalCycles()
        << ","
        << "\"accepted\":"
        << sequence.getAcceptedCycles()
        << ","
        << "\"rejected\":"
        << sequence.getRejectedCycles()
        << "}"
        << "}"
        << std::endl;
}

} // namespace

int main()
{
    workcell::SimRobotArm robot;
    workcell::SimConveyor conveyor;
    workcell::SimGripper gripper;
    workcell::SimPartSensor sensor;

    robot.initialize();
    conveyor.initialize();
    gripper.initialize();
    sensor.initialize();

    workcell::SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    workcell::SafetyController safety(
        robot,
        conveyor
    );

    workcell::FaultManager faultManager;

    workcell::MachineController machine(
        sequence,
        safety,
        faultManager
    );

    std::string command;

    while (std::getline(std::cin, command))
    {
        bool success = false;

        if (command == "status")
        {
            success = true;
        }
        else if (command == "initialize")
        {
            success =
                machine.initialize();
        }
        else if (command == "start")
        {
            success =
                machine.start();
        }
        else if (command == "pause")
        {
            success =
                machine.pause();
        }
        else if (command == "resume")
        {
            success =
                machine.resume();
        }
        else if (command == "stop")
        {
            success =
                machine.stop();
        }
        else if (command == "reset")
        {
            success =
                machine.reset();
        }
        else if (command == "estop")
        {
            success =
                machine.emergencyStop();
        }
        else if (command == "clear-estop")
        {
            success =
                machine.clearEmergencyStop();
        }
        else if (command == "fault-motion-timeout")
        {
            success =
                machine.triggerFault(
                    workcell::FaultCode::MotionTimeout,
                    "Injected motion timeout"
                );
        }
        else if (command == "exit")
        {
            sendResponse(
                true,
                machine,
                sequence
            );

            break;
        }
        else
        {
            success = false;
        }

        machine.update();

        sendResponse(
            success,
            machine,
            sequence
        );
    }

    return 0;
}