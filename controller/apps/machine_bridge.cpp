#include "faults/FaultManager.hpp"
#include "inspection/PgmInspectionSystem.hpp"
#include "machine/MachineController.hpp"
#include "machine/MachineState.hpp"
#include "safety/SafetyController.hpp"
#include "sequence/SequenceController.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

#include <iostream>
#include <cctype>
#include <condition_variable>
#include <functional>
#include <future>
#include <iomanip>
#include <mutex>
#include <optional>
#include <queue>
#include <sstream>
#include <string>
#include <thread>
#include <utility>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

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
        << "},";

    if (sequence.getInspectionResult().has_value())
    {
        const auto& inspection = sequence.getInspectionResult().value();
        output
            << "\"inspection\":{"
            << "\"state\":\"Complete\","
            << "\"accepted\":" << (inspection.accepted ? "true" : "false") << ","
            << "\"reason\":\"" << workcell::toString(inspection.reason) << "\","
            << "\"sampleId\":\"" << escapeJson(inspection.sampleId) << "\","
            << "\"featureCoverage\":" << inspection.featureCoverage << ","
            << "\"details\":\"" << escapeJson(inspection.details) << "\"},";
    }
    else
    {
        output << "\"inspection\":{\"state\":\"Idle\"},";
    }

    output
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

std::optional<std::size_t> jsonPropertyValue(
    const std::string& json,
    const std::string& property
)
{
    const auto key = "\"" + property + "\"";
    auto position = std::size_t{0};
    while ((position = json.find(key, position)) != std::string::npos)
    {
        auto separator = position + key.size();
        while (separator < json.size() && std::isspace(
            static_cast<unsigned char>(json[separator])))
        {
            ++separator;
        }
        if (separator < json.size() && json[separator] == ':')
        {
            return separator + 1;
        }
        position += key.size();
    }
    return std::nullopt;
}

std::optional<std::string> jsonString(
    const std::string& json,
    const std::string& property
)
{
    const auto valuePosition = jsonPropertyValue(json, property);
    if (!valuePosition.has_value())
    {
        return std::nullopt;
    }
    auto position = json.find('"', valuePosition.value());
    if (position == std::string::npos)
    {
        return std::nullopt;
    }
    std::string value;
    bool escaped = false;
    for (++position; position < json.size(); ++position)
    {
        const char character = json[position];
        if (escaped)
        {
            if (character != '"' && character != '\\')
            {
                return std::nullopt;
            }
            value += character;
            escaped = false;
        }
        else if (character == '\\')
        {
            escaped = true;
        }
        else if (character == '"')
        {
            return value;
        }
        else
        {
            value += character;
        }
    }
    return std::nullopt;
}

bool jsonBoolean(
    const std::string& json,
    const std::string& property,
    bool defaultValue
)
{
    const auto valuePosition = jsonPropertyValue(json, property);
    if (!valuePosition.has_value())
    {
        return defaultValue;
    }
    const auto value = json.substr(valuePosition.value());
    return value.find("true") < value.find("false");
}

std::string protocolResponse(
    const std::string& requestId,
    bool success,
    const std::string& status,
    const std::string& errorCode = {},
    const std::string& errorMessage = {}
)
{
    std::ostringstream output;
    output << "{\"requestId\":\"" << escapeJson(requestId)
        << "\",\"success\":" << (success ? "true" : "false")
        << ",\"status\":" << status << ",\"error\":";
    if (errorCode.empty())
    {
        output << "null";
    }
    else
    {
        output << "{\"code\":\"" << escapeJson(errorCode)
            << "\",\"message\":\"" << escapeJson(errorMessage) << "\"}";
    }
    output << "}";
    return output.str();
}

#ifdef _WIN32
using SocketHandle = SOCKET;
constexpr SocketHandle INVALID_SOCKET_HANDLE = INVALID_SOCKET;
void closeSocket(SocketHandle socket) { closesocket(socket); }
#else
using SocketHandle = int;
constexpr SocketHandle INVALID_SOCKET_HANDLE = -1;
void closeSocket(SocketHandle socket) { close(socket); }
#endif

bool sendAll(SocketHandle socket, const std::string& message)
{
    std::size_t sent = 0;
    while (sent < message.size())
    {
        const auto count = send(
            socket,
            message.data() + sent,
            static_cast<int>(message.size() - sent),
            0
        );
        if (count <= 0)
        {
            return false;
        }
        sent += static_cast<std::size_t>(count);
    }
    return true;
}

} // namespace

int main(int argc, char* argv[])
{
    workcell::SimRobotArm robot;
    workcell::SimConveyor conveyor;
    workcell::SimGripper gripper;
    workcell::SimPartSensor sensor;
    workcell::PgmInspectionSystem inspection(
        WORKCELL_INSPECTION_SAMPLE_DIR
    );

    robot.initialize();
    conveyor.initialize();
    gripper.initialize();
    sensor.initialize();

    workcell::SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor,
        &inspection
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
        [&](std::string command, bool legacyOutput = false)
        {
            auto response =
                std::make_shared<std::promise<std::pair<std::string, std::string>>>();
            auto result = response->get_future();

            {
                std::lock_guard lock(queueMutex);
                commands.push(
                    [&, command = std::move(command), response, legacyOutput]
                    {
                        bool success = false;
                        bool knownCommand = true;

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
                        else if (command.starts_with("cycle-sample-"))
                        {
                            const auto sampleId = command.substr(
                                std::string("cycle-sample-").size()
                            );
                            if (
                                machine.getState() == workcell::MachineState::Running
                                && inspection.isKnownSample(sampleId)
                            )
                            {
                                sensor.setActive(true);
                                success = machine.startProductionCycle(sampleId);
                                if (!success)
                                {
                                    sensor.setActive(false);
                                }
                            }
                        }
                        else
                        {
                            knownCommand = false;
                        }

                        auto error = !knownCommand
                            ? std::string("UNKNOWN_COMMAND")
                            : !success
                                ? std::string("COMMAND_REJECTED")
                                : std::string();
                        auto status = makeResponse(
                            success, machine, sequence, robot, conveyor, gripper, sensor
                        );
                        if (legacyOutput)
                        {
                            std::cout << RESPONSE_PREFIX << status << std::endl;
                        }
                        response->set_value({std::move(status), error});
                    }
                );
            }

            queueChanged.notify_one();
            return result.get();
        };

    int tcpPort = 0;
    if (argc == 3 && std::string(argv[1]) == "--tcp-port")
    {
        try
        {
            tcpPort = std::stoi(argv[2]);
        }
        catch (...)
        {
            return 2;
        }
    }

    if (tcpPort > 0)
    {
#ifdef _WIN32
        WSADATA socketData{};
        if (WSAStartup(MAKEWORD(2, 2), &socketData) != 0)
        {
            return 3;
        }
#endif
        const auto server = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (server == INVALID_SOCKET_HANDLE)
        {
            return 3;
        }
        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_port = htons(static_cast<unsigned short>(tcpPort));
        address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
        int reuse = 1;
        setsockopt(server, SOL_SOCKET, SO_REUSEADDR,
            reinterpret_cast<const char*>(&reuse), sizeof(reuse));
        if (bind(server, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0
            || listen(server, 1) != 0)
        {
            closeSocket(server);
            return 3;
        }

        const auto client = accept(server, nullptr, nullptr);
        closeSocket(server);
        if (client == INVALID_SOCKET_HANDLE)
        {
            return 3;
        }

        std::string buffer;
        char incoming[4096];
        bool stopServer = false;
        while (!stopServer)
        {
            const auto count = recv(client, incoming, sizeof(incoming), 0);
            if (count <= 0)
            {
                break;
            }
            buffer.append(incoming, static_cast<std::size_t>(count));
            if (buffer.size() > 65536)
            {
                sendAll(client, protocolResponse("", false, "null", "MESSAGE_TOO_LARGE", "Request exceeded 64 KiB." ) + "\n");
                break;
            }
            std::size_t newline = 0;
            while ((newline = buffer.find('\n')) != std::string::npos)
            {
                auto message = buffer.substr(0, newline);
                buffer.erase(0, newline + 1);
                if (!message.empty() && message.back() == '\r')
                {
                    message.pop_back();
                }
                const auto requestId = jsonString(message, "requestId");
                const auto protocolCommand = jsonString(message, "command");
                if (!requestId.has_value() || !protocolCommand.has_value()
                    || message.empty() || message.front() != '{' || message.back() != '}')
                {
                    if (!sendAll(client, protocolResponse(
                        requestId.value_or(""), false, "null", "MALFORMED_REQUEST",
                        "Request must be newline-delimited JSON with requestId and command.") + "\n"))
                    {
                        stopServer = true;
                    }
                    continue;
                }

                std::string command = protocolCommand.value();
                if (command == "start-cycle")
                {
                    const auto sampleId = jsonString(message, "sampleId");
                    command = sampleId.has_value()
                        ? "cycle-sample-" + sampleId.value()
                        : "";
                }
                else if (command == "configure-simulation-fault")
                {
                    const auto fault = jsonString(message, "fault");
                    const bool enabled = jsonBoolean(message, "enabled", true);
                    command = fault.has_value()
                        ? "simulation-fault-" + fault.value() + (enabled ? "" : "-clear")
                        : "";
                }
                else if (command == "shutdown")
                {
                    command = "status";
                    stopServer = true;
                }
                else if (command == "diagnostic-delay")
                {
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                    command = "status";
                }

                const auto [status, error] = submit(command);
                const bool success = error.empty();
                if (!sendAll(client, protocolResponse(
                    requestId.value(), success, status, error,
                    error == "UNKNOWN_COMMAND" ? "Unknown IPC command." : "Controller rejected the command.") + "\n"))
                {
                    stopServer = true;
                }
            }
        }
        closeSocket(client);
#ifdef _WIN32
        WSACleanup();
#endif
    }
    else
    {
        std::string command;
        while (std::getline(std::cin, command))
        {
            if (command == "exit")
            {
                submit("status", true);
                break;
            }

            submit(command, true);
        }
    }

    {
        std::lock_guard lock(queueMutex);
        shuttingDown = true;
    }

    queueChanged.notify_one();
    controllerThread.join();

    return 0;
}
