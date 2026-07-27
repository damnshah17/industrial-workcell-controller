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

    workcell::SimRobotArm robot(500ms);
    workcell::SimConveyor conveyor;
    workcell::SimGripper gripper;
    workcell::SimPartSensor partSensor;

    robot.initialize();
    conveyor.initialize();
    gripper.initialize();
    partSensor.initialize();

    conveyor.start();

    workcell::SequenceController sequence(
        robot,
        conveyor,
        gripper,
        partSensor
    );

    std::cout
        << "\n=== Industrial Workcell Cycle Demo ===\n\n";

    partSensor.setActive(true);

    if (!sequence.startCycle(true))
    {
        std::cout
            << "Unable to start cycle.\n";

        return 1;
    }

    while (
        sequence.getState()
            != workcell::CycleState::CycleComplete
        &&
        sequence.getState()
            != workcell::CycleState::CycleFaulted
    )
    {
        sequence.update();

        std::this_thread::sleep_for(
            50ms
        );
    }

    if (
        sequence.getState()
        == workcell::CycleState::CycleFaulted
    )
    {
        std::cout
            << "\nCycle faulted.\n";

        return 1;
    }

    std::cout
        << "\nCycle statistics\n"
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