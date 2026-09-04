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
#include <condition_variable>
#include <functional>
#include <future>
#include <iomanip>
#include <mutex>
#include <queue>
#include <sstream>
#include <string>
#include <thread>

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
            if (
                static_cast<unsigned char>(character)
                < 0x20
            )
            {
                std::ostringstream escaped;
                escaped
                    << "\\u"
                    << std::hex
                    << std::setw(4)
                    << std::setfill('0')
                    << static_cast<int>(
                        static_cast<unsigned char>(character)
                    );
                result += escaped.str();
            }
            else
            {
                result += character;
            }
            break;
        }
    }

    return result;
}

std::string makeResponse(
    bool success,
    const workcell::MachineController& machine,
    const workcell::SequenceController& sequence,
    const workcell::SimRobotArm& robot,
    const workcell::SimConveyor& conveyor,
    const workcell::SimGripper& gripper,
    const workcell::SimPartSensor& sensor
)
{
    std::ostringstream output;

    output
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

        output
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
        output
            << ",\"fault\":null";
    }

    output
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
        << "},"
        << "\"robot\":{"
        << "\"position\":\""
        << workcell::toString(robot.getPosition())
        << "\",\"moving\":"
        << (robot.isMoving() ? "true" : "false")
        << ",\"initialized\":"
        << (robot.isInitialized() ? "true" : "false")
        << "},"
        << "\"conveyor\":{\"running\":"
        << (conveyor.isRunning() ? "true" : "false")
        << "},"
        << "\"gripper\":{\"open\":"
        << (gripper.isOpen() ? "true" : "false")
        << "},"
        << "\"partSensor\":{\"active\":"
        << (sensor.isActive() ? "true" : "false")
        << "}"
        << "}";

    return output.str();
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

    auto configureSimulationFault =
        [&](const std::string& fault, bool enabled)
        {
            if (fault == "robot-communication")
            {
                robot.setCommunicationFailure(enabled);
            }
            else if (fault == "motion-timeout")
            {
                robot.setMotionStalled(enabled);
                sequence.setMotionTimeout(
                    enabled
                        ? std::chrono::milliseconds(200)
                        : std::chrono::milliseconds(3000)
                );
            }
            else if (fault == "conveyor-start")
            {
                conveyor.setStartFailure(enabled);
            }
            else if (fault == "conveyor-stop")
            {
                conveyor.setStopFailure(enabled);
            }
            else if (fault == "gripper-open")
            {
                gripper.setOpenFailure(enabled);
            }
            else if (fault == "gripper-close")
            {
                gripper.setCloseFailure(enabled);
            }
            else if (fault == "sensor")
            {
                sensor.setFailure(enabled);
            }
            else if (fault == "safety-door")
            {
                safety.setSafetyDoorOpen(enabled);
            }
            else
            {
                return false;
            }

            return true;
        };

    auto clearAllSimulationFaults = [&]
        {
            configureSimulationFault("robot-communication", false);
            configureSimulationFault("motion-timeout", false);
            configureSimulationFault("conveyor-start", false);
            configureSimulationFault("conveyor-stop", false);
            configureSimulationFault("gripper-open", false);
            configureSimulationFault("gripper-close", false);
            configureSimulationFault("sensor", false);
            configureSimulationFault("safety-door", false);
        };

    std::mutex queueMutex;
    std::condition_variable queueChanged;
    std::queue<std::function<void()>> commands;
    bool shuttingDown = false;

    std::thread controllerThread(
        [&]
        {
            using namespace std::chrono_literals;

            while (true)
            {
                std::queue<std::function<void()>> pending;

                {
                    std::unique_lock lock(queueMutex);
                    queueChanged.wait_for(
                        lock,
                        10ms,
                        [&]
                        {
                            return shuttingDown
                                || !commands.empty();
                        }
                    );

                    pending.swap(commands);

                    if (shuttingDown && pending.empty())
                    {
                        break;
                    }
                }

                while (!pending.empty())
                {
                    pending.front()();
                    pending.pop();
                }

                machine.update();

                if (
                    sequence.getState()
                        == workcell::CycleState::CycleComplete
                    && sensor.isActive()
                )
                {
                    sensor.setActive(false);
                }
            }
        }
    );

    auto submit =
        [&](std::string command)
        {
            auto response =
                std::make_shared<std::promise<void>>();
            auto result = response->get_future();

            {
                std::lock_guard lock(queueMutex);
                commands.push(
                    [&, command = std::move(command), response]
                    {
                        bool success = false;

                        if (command == "status")
                        {
                            success = true;
                        }
                        else if (command == "initialize")
                        {
                            success = machine.initialize();
                        }
                        else if (command == "start")
                        {
                            success = machine.start();
                        }
                        else if (command == "pause")
                        {
                            success = machine.pause();
                        }
                        else if (command == "resume")
                        {
                            success = machine.resume();
                        }
                        else if (command == "stop")
                        {
                            success = machine.stop();
                        }
                        else if (command == "reset")
                        {
                            success = machine.reset();
                        }
                        else if (command == "estop")
                        {
                            success = machine.emergencyStop();
                        }
                        else if (command == "clear-estop")
                        {
                            success = machine.clearEmergencyStop();
                        }
                        else if (command == "fault-motion-timeout")
                        {
                            success = machine.triggerFault(
                                workcell::FaultCode::MotionTimeout,
                                "Injected motion timeout"
                            );
                        }
                        else if (command == "simulation-faults-clear")
                        {
                            clearAllSimulationFaults();
                            success = true;
                        }
                        else if (
                            command.starts_with("simulation-fault-")
                        )
                        {
                            constexpr const char* prefix =
                                "simulation-fault-";
                            const auto clearSuffix =
                                std::string("-clear");
                            auto fault = command.substr(
                                std::char_traits<char>::length(prefix)
                            );
                            bool enabled = true;

                            if (
                                fault.size() > clearSuffix.size()
                                && fault.ends_with(clearSuffix)
                            )
                            {
                                enabled = false;
                                fault.erase(
                                    fault.size() - clearSuffix.size()
                                );
                            }

                            success = configureSimulationFault(
                                fault,
                                enabled
                            );
                        }
                        else if (
                            command == "cycle-accepted"
                            || command == "cycle-rejected"
                        )
                        {
                            if (
                                machine.getState()
                                == workcell::MachineState::Running
                            )
                            {
                                sensor.setActive(true);
                                success = machine.startProductionCycle(
                                    command == "cycle-accepted"
                                );

                                if (!success)
                                {
                                    sensor.setActive(false);
                                }
                            }
                        }

                        std::cout
                            << makeResponse(
                                success,
                                machine,
                                sequence,
                                robot,
                                conveyor,
                                gripper,
                                sensor
                            )
                            << std::endl;

                        response->set_value();
                    }
                );
            }

            queueChanged.notify_one();
            result.get();
        };

    std::string command;

    while (std::getline(std::cin, command))
    {
        if (command == "exit")
        {
            submit("status");
            break;
        }

        submit(command);
    }

    {
        std::lock_guard lock(queueMutex);
        shuttingDown = true;
    }

    queueChanged.notify_one();
    controllerThread.join();

    return 0;
}
