#include "sequence/SequenceController.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

#include <iostream>

int main()
{
    workcell::SimRobotArm robot;
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

    std::cout << "Simulating part arrival...\n";

    partSensor.setActive(true);

    std::cout << "\nInspection result: PASS\n\n";

    if (!sequence.runCycle(true))
    {
        std::cout << "Cycle failed.\n";
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