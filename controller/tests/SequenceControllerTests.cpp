#include <gtest/gtest.h>

#include "hardware/RobotPosition.hpp"
#include "sequence/CycleState.hpp"
#include "sequence/SequenceController.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

#include <chrono>
#include <thread>

using namespace std::chrono_literals;

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

void runUntilFinished(
    SequenceController& sequence,
    std::chrono::milliseconds maximumDuration =
        2s
)
{
    const auto start =
        std::chrono::steady_clock::now();

    while (
        sequence.getState()
            != CycleState::CycleComplete
        &&
        sequence.getState()
            != CycleState::CycleFaulted
    )
    {
        sequence.update();

        std::this_thread::sleep_for(
            1ms
        );

        if (
            std::chrono::steady_clock::now()
            - start
            > maximumDuration
        )
        {
            FAIL()
                << "Sequence did not finish in expected time.";
        }
    }
}

}

TEST(
    SequenceControllerTest,
    StartsWaitingForPart
)
{
    SimRobotArm robot(1ms);
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
    CannotStartWithUninitializedHardware
)
{
    SimRobotArm robot(1ms);
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
        sequence.startCycle(true)
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::CycleFaulted
    );
}

TEST(
    SequenceControllerTest,
    DoesNotStartWithoutPart
)
{
    SimRobotArm robot(1ms);
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
        sequence.startCycle(true)
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::WaitingForPart
    );
}

TEST(
    SequenceControllerTest,
    AcceptedPartCompletesCycle
)
{
    SimRobotArm robot(1ms);
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

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    runUntilFinished(sequence);

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
    RejectedPartCompletesCycle
)
{
    SimRobotArm robot(1ms);
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

    ASSERT_TRUE(
        sequence.startCycle(false)
    );

    runUntilFinished(sequence);

    EXPECT_EQ(
        sequence.getState(),
        CycleState::CycleComplete
    );

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
    CompletedCycleCanReset
)
{
    SimRobotArm robot(1ms);
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

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    runUntilFinished(sequence);

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
    RobotMotionTimeoutFaultsCycle
)
{
    SimRobotArm robot(500ms);
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

    sequence.setMotionTimeout(
        10ms
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    runUntilFinished(
        sequence,
        500ms
    );

    EXPECT_EQ(
        sequence.getState(),
        CycleState::CycleFaulted
    );

    EXPECT_FALSE(
        conveyor.isRunning()
    );

    EXPECT_FALSE(
        robot.isMoving()
    );
}