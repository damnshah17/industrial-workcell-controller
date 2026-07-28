#include "faults/Fault.hpp"
#include "faults/FaultManager.hpp"

#include "machine/Command.hpp"
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

void printHelp()
{
    std::cout << "\nAvailable commands:\n";
    std::cout << "  status        Show machine status\n";
    std::cout << "  initialize    Initialize machine\n";
    std::cout << "  start         Start production\n";
    std::cout << "  pause         Pause production\n";
    std::cout << "  resume        Resume production\n";
    std::cout << "  stop          Stop production\n";
    std::cout << "  reset         Reset faulted/E-stop machine\n";
    std::cout << "  estop         Activate Emergency Stop\n";
    std::cout << "  clear-estop   Clear Emergency Stop condition\n";
    std::cout << "  fault <text>  Inject simulated motion fault\n";
    std::cout << "  help          Show commands\n";
    std::cout << "  exit          Exit controller\n\n";
}

void printStatus(
    const workcell::MachineController& controller
)
{
    std::cout
        << "\nMachine State: "
        << workcell::toString(controller.getState())
        << '\n';

    std::cout
        << "Emergency Stop: "
        << (
            controller.isEmergencyStopActive()
                ? "ACTIVE"
                : "CLEAR"
        )
        << '\n';

    if (controller.hasActiveFault())
    {
        const auto& fault =
            controller.getActiveFault().value();

        std::cout
            << "Active Fault: "
            << workcell::toString(fault.code)
            << '\n';

        std::cout
            << "Fault Message: "
            << fault.message
            << '\n';
    }
    else
    {
        std::cout
            << "Active Fault: None\n";
    }

    std::cout << '\n';
}

}

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

    workcell::MachineController controller(
        sequence,
        safety,
        faultManager
    );

    std::cout
        << "=====================================\n"
        << " Industrial Robotic Workcell Control\n"
        << "=====================================\n";

    printHelp();

    std::string input;

    while (true)
    {
        std::cout
            << "["
            << workcell::toString(
                controller.getState()
            )
            << "] > ";

        if (!std::getline(
                std::cin,
                input
            ))
        {
            break;
        }

        if (input.empty())
        {
            continue;
        }

        const workcell::Command command =
            workcell::parseCommand(
                input
            );

        switch (command.type)
        {
            case workcell::CommandType::Status:
                printStatus(
                    controller
                );
                break;

            case workcell::CommandType::Initialize:
                controller.initialize();
                break;

            case workcell::CommandType::Start:
                controller.start();
                break;

            case workcell::CommandType::Pause:
                controller.pause();
                break;

            case workcell::CommandType::Resume:
                controller.resume();
                break;

            case workcell::CommandType::Stop:
                controller.stop();
                break;

            case workcell::CommandType::Reset:
                controller.reset();
                break;

            case workcell::CommandType::EmergencyStop:
                controller.emergencyStop();
                break;

            case workcell::CommandType::ClearEmergencyStop:
                controller.clearEmergencyStop();
                break;

            case workcell::CommandType::InjectFault:
            {
                const std::string message =
                    command.argument.empty()
                        ? "Simulated robot motion timeout"
                        : command.argument;

                if (
                    controller.triggerFault(
                        workcell::FaultCode::MotionTimeout,
                        message
                    )
                )
                {
                    std::cout
                        << "[CLI] Simulated fault injected.\n";
                }

                break;
            }

            case workcell::CommandType::Help:
                printHelp();
                break;

            case workcell::CommandType::Exit:
                std::cout
                    << "Controller shutting down.\n";

                return 0;

            case workcell::CommandType::Invalid:
                std::cout
                    << "[CLI] Unknown command: "
                    << input
                    << '\n';

                break;
        }
    }

    return 0;
}