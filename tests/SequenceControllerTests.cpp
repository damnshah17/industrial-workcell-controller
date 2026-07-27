#include <gtest/gtest.h>

#include "hardware/RobotPosition.hpp"
#include "sequence/CycleState.hpp"
#include "sequence/SequenceController.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

using workcell::CycleState;
using workcell::RobotPosition;
using workcell::SequenceController;
using workcell::SimConveyor;
using workcell::SimGripper;
using workcell::SimPartSensor;
using workcell::SimRobotArm;

namespace {

void initializeHardware(
    SimRobotArm& robot,
    SimConveyor& conveyor,
    SimGripper& gripper,
    SimPartSensor& sensor
)
{
    robot.initialize();
    conveyor.initialize();
    gripper.initialize();
    sensor.initialize();

    conveyor.start();
}

}

TEST(
    SequenceControllerTest,
    StartsWaitingForPart
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::WaitingForPart
    );
}

TEST(
    SequenceControllerTest,
    CannotRunWithUninitializedHardware
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    EXPECT_FALSE(
        sequence.runCycle(true)
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::CycleFaulted
    );
}

TEST(
    SequenceControllerTest,
    DoesNotStartCycleWithoutPart
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    EXPECT_FALSE(
        sequence.runCycle(true)
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::WaitingForPart
    );

    EXPECT_TRUE(
        conveyor.isRunning()
    );
}

TEST(
    SequenceControllerTest,
    AcceptedPartCompletesCycle
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sensor.setActive(true);

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    EXPECT_TRUE(
        sequence.runCycle(true)
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::CycleComplete
    );

    EXPECT_EQ(
        robot.getPosition(),
        RobotPosition::Home
    );

    EXPECT_TRUE(
        gripper.isOpen()
    );

    EXPECT_TRUE(
        conveyor.isRunning()
    );
}

TEST(
    SequenceControllerTest,
    RejectedPartCompletesCycle
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sensor.setActive(true);

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    EXPECT_TRUE(
        sequence.runCycle(false)
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::CycleComplete
    );

    EXPECT_EQ(
        robot.getPosition(),
        RobotPosition::Home
    );

    EXPECT_TRUE(
        gripper.isOpen()
    );

    EXPECT_TRUE(
        conveyor.isRunning()
    );
}

TEST(
    SequenceControllerTest,
    AcceptedCycleUpdatesProductionCounters
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sensor.setActive(true);

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sequence.runCycle(true);

    EXPECT_EQ(
        sequence.getTotalCycles(),
        1U
    );

    EXPECT_EQ(
        sequence.getAcceptedCycles(),
        1U
    );

    EXPECT_EQ(
        sequence.getRejectedCycles(),
        0U
    );
}

TEST(
    SequenceControllerTest,
    RejectedCycleUpdatesProductionCounters
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sensor.setActive(true);

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sequence.runCycle(false);

    EXPECT_EQ(
        sequence.getTotalCycles(),
        1U
    );

    EXPECT_EQ(
        sequence.getAcceptedCycles(),
        0U
    );

    EXPECT_EQ(
        sequence.getRejectedCycles(),
        1U
    );
}

TEST(
    SequenceControllerTest,
    CompletedCycleCanResetForNextPart
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sensor.setActive(true);

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    sequence.runCycle(true);

    EXPECT_TRUE(
        sequence.resetForNextCycle()
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::WaitingForPart
    );
}

TEST(
    SequenceControllerTest,
    CannotResetIncompleteCycle
)
{
    SimRobotArm robot;
    SimConveyor conveyor;
    SimGripper gripper;
    SimPartSensor sensor;

    initializeHardware(
        robot,
        conveyor,
        gripper,
        sensor
    );

    SequenceController sequence(
        robot,
        conveyor,
        gripper,
        sensor
    );

    EXPECT_FALSE(
        sequence.resetForNextCycle()
    );
}