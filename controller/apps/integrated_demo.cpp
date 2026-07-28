#include "faults/FaultManager.hpp"
#include "machine/MachineController.hpp"
#include "machine/MachineState.hpp"
#include "safety/SafetyController.hpp"
#include "sequence/CycleState.hpp"
#include "sequence/SequenceController.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

#include <chrono>
#include <iostream>
#include <thread>

int main()
{
    using namespace std::chrono_literals;

    workcell::SimRobotArm robot(300ms);
    workcell::SimConveyor conveyor;
    workcell::SimGripper gripper;
    workcell::SimPartSensor sensor;

    if (
        !robot.initialize()
        || !conveyor.initialize()
        || !gripper.initialize()
        || !sensor.initialize()
    )
    {
        std::cerr
            << "Hardware initialization failed.\n";

        return 1;
    }

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

    std::cout
        << "\n=====================================\n"
        << " Integrated Industrial Workcell Demo\n"
        << "=====================================\n\n";

    if (!machine.initialize())
    {
        std::cerr
            << "Machine initialization failed.\n";

        return 1;
    }

    if (!conveyor.start())
    {
        std::cerr
            << "Unable to start conveyor.\n";

        return 1;
    }

    if (!machine.start())
    {
        std::cerr
            << "Unable to start machine.\n";

        return 1;
    }

    sensor.setActive(true);

    if (!sequence.startCycle(true))
    {
        std::cerr
            << "Unable to start production cycle.\n";

        return 1;
    }

    std::cout
        << "\nProduction cycle running...\n\n";

    const auto startTime =
        std::chrono::steady_clock::now();

    while (
        machine.getState()
        == workcell::MachineState::Running
        &&
        sequence.getState()
        != workcell::CycleState::CycleComplete
    )
    {
        machine.update();

        std::this_thread::sleep_for(
            50ms
        );

        if (
            std::chrono::steady_clock::now()
                - startTime
            > 10s
        )
        {
            std::cerr
                << "Demo timed out.\n";

            return 1;
        }
    }

    if (
        machine.getState()
        == workcell::MachineState::Faulted
    )
    {
        std::cerr
            << "\nMachine faulted.\n";

        if (machine.hasActiveFault())
        {
            const auto& fault =
                machine.getActiveFault().value();

            std::cerr
                << "Fault: "
                << workcell::toString(
                    fault.code
                )
                << "\nMessage: "
                << fault.message
                << '\n';
        }

        return 1;
    }

    if (
        sequence.getState()
        != workcell::CycleState::CycleComplete
    )
    {
        std::cerr
            << "Cycle did not complete successfully.\n";

        return 1;
    }

    std::cout
        << "\nProduction cycle complete.\n\n"
        << "Machine State: "
        << workcell::toString(
            machine.getState()
        )
        << '\n'
        << "Cycle State: "
        << workcell::toString(
            sequence.getState()
        )
        << "\n\n"
        << "Cycle Statistics\n"
        << "----------------\n"
        << "Total:    "
        << sequence.getTotalCycles()
        << '\n'
        << "Accepted: "
        << sequence.getAcceptedCycles()
        << '\n'
        << "Rejected: "
        << sequence.getRejectedCycles()
        << '\n';

    return 0;
}