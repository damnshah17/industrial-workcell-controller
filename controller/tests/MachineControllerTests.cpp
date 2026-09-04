#include <gtest/gtest.h>

#include "faults/FaultManager.hpp"
#include "hardware/RobotPosition.hpp"
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
#include <thread>

using namespace std::chrono_literals;

namespace {

class MachineControllerTest
    : public ::testing::Test
{
protected:
    workcell::SimRobotArm robot{1ms};

    workcell::SimConveyor conveyor;
    workcell::SimGripper gripper;
    workcell::SimPartSensor sensor;

    workcell::SequenceController sequence{
        robot,
        conveyor,
        gripper,
        sensor
    };

    workcell::SafetyController safety{
        robot,
        conveyor
    };

    workcell::FaultManager faults;

    workcell::MachineController controller{
        sequence,
        safety,
        faults
    };

    void SetUp() override
    {
        ASSERT_TRUE(robot.initialize());
        ASSERT_TRUE(conveyor.initialize());
        ASSERT_TRUE(gripper.initialize());
        ASSERT_TRUE(sensor.initialize());
    }
};

} // namespace

TEST_F(
    MachineControllerTest,
    StartsOffline
)
{
    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Offline
    );
}

TEST_F(
    MachineControllerTest,
    InitializeMovesMachineToIdle
)
{
    EXPECT_TRUE(
        controller.initialize()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Idle
    );
}

TEST_F(
    MachineControllerTest,
    CannotStartWhileOffline
)
{
    EXPECT_FALSE(
        controller.start()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Offline
    );
}

TEST_F(
    MachineControllerTest,
    StartFromIdleMovesToRunning
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    EXPECT_TRUE(
        controller.start()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Running
    );
}

TEST_F(
    MachineControllerTest,
    RunningMachineCanPause
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    EXPECT_TRUE(
        controller.pause()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Paused
    );
}

TEST_F(
    MachineControllerTest,
    PausedMachineCanResume
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        controller.pause()
    );

    EXPECT_TRUE(
        controller.resume()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Running
    );
}

TEST_F(
    MachineControllerTest,
    RunningMachineCanStop
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    EXPECT_TRUE(
        controller.stop()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Idle
    );
}

TEST_F(
    MachineControllerTest,
    CannotPauseIdleMachine
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    EXPECT_FALSE(
        controller.pause()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Idle
    );
}

TEST_F(
    MachineControllerTest,
    CannotInitializeRunningMachine
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    EXPECT_FALSE(
        controller.initialize()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Running
    );
}

TEST_F(
    MachineControllerTest,
    EmergencyStopInterruptsRunningMachine
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    EXPECT_TRUE(
        controller.emergencyStop()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::EmergencyStop
    );

    EXPECT_TRUE(
        controller.isEmergencyStopActive()
    );
}

TEST_F(
    MachineControllerTest,
    EmergencyStopCannotResetWhileStillActive
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        controller.emergencyStop()
    );

    EXPECT_FALSE(
        controller.reset()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::EmergencyStop
    );
}

TEST_F(
    MachineControllerTest,
    EmergencyStopCanResetAfterConditionCleared
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        controller.emergencyStop()
    );

    EXPECT_TRUE(
        controller.clearEmergencyStop()
    );

    EXPECT_TRUE(
        controller.reset()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Idle
    );
}

TEST_F(
    MachineControllerTest,
    EmergencyStopStopsActiveRobotAndConveyor
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    robot.setMotionDuration(
        500ms
    );

    ASSERT_TRUE(
        robot.moveTo(
            workcell::RobotPosition::Pick
        )
    );

    ASSERT_TRUE(
        robot.isMoving()
    );

    ASSERT_TRUE(
        conveyor.isRunning()
    );

    EXPECT_TRUE(
        controller.emergencyStop()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::EmergencyStop
    );

    EXPECT_FALSE(
        robot.isMoving()
    );

    EXPECT_FALSE(
        conveyor.isRunning()
    );
}

TEST_F(
    MachineControllerTest,
    SequenceFaultMovesMachineToFaulted
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    robot.setMotionDuration(
        500ms
    );

    sequence.setMotionTimeout(
        10ms
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    const auto start =
        std::chrono::steady_clock::now();

    while (
        controller.getState()
        == workcell::MachineState::Running
    )
    {
        controller.update();

        std::this_thread::sleep_for(
            1ms
        );

        ASSERT_LT(
            std::chrono::steady_clock::now()
                - start,
            1s
        );
    }

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    ASSERT_TRUE(
        controller.hasActiveFault()
    );

    ASSERT_TRUE(
        controller.getActiveFault().has_value()
    );

    EXPECT_EQ(
        controller.getActiveFault()->code,
        workcell::FaultCode::MotionTimeout
    );

    EXPECT_EQ(
        sequence.getState(),
        workcell::CycleState::CycleFaulted
    );

    EXPECT_FALSE(
        robot.isMoving()
    );

    EXPECT_FALSE(
        conveyor.isRunning()
    );
}

TEST_F(
    MachineControllerTest,
    FaultedMachineCannotStart
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    robot.setMotionDuration(
        500ms
    );

    sequence.setMotionTimeout(
        10ms
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    const auto start =
        std::chrono::steady_clock::now();

    while (
        controller.getState()
        == workcell::MachineState::Running
    )
    {
        controller.update();

        std::this_thread::sleep_for(
            1ms
        );

        ASSERT_LT(
            std::chrono::steady_clock::now()
                - start,
            1s
        );
    }

    ASSERT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    EXPECT_FALSE(
        controller.start()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );
}

TEST_F(
    MachineControllerTest,
    ResetClearsFaultAndReturnsMachineToIdle
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    robot.setMotionDuration(
        500ms
    );

    sequence.setMotionTimeout(
        10ms
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    const auto start =
        std::chrono::steady_clock::now();

    while (
        controller.getState()
        == workcell::MachineState::Running
    )
    {
        controller.update();

        std::this_thread::sleep_for(
            1ms
        );

        ASSERT_LT(
            std::chrono::steady_clock::now()
                - start,
            1s
        );
    }

    ASSERT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    ASSERT_TRUE(
        controller.hasActiveFault()
    );

    EXPECT_TRUE(
        controller.reset()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Idle
    );

    EXPECT_FALSE(
        controller.hasActiveFault()
    );

    EXPECT_EQ(
        sequence.getState(),
        workcell::CycleState::WaitingForPart
    );
}

TEST_F(
    MachineControllerTest,
    InjectedFaultMovesMachineToFaulted
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    EXPECT_TRUE(
        controller.triggerFault(
            workcell::FaultCode::MotionTimeout,
            "Injected motion timeout"
        )
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    ASSERT_TRUE(
        controller.hasActiveFault()
    );

    EXPECT_EQ(
        controller.getActiveFault()->code,
        workcell::FaultCode::MotionTimeout
    );
}

TEST_F(
    MachineControllerTest,
    StopAbortsAndResetsActiveSequence
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    robot.setMotionDuration(
        500ms
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    controller.update();

    ASSERT_EQ(
        sequence.getState(),
        workcell::CycleState::MovingToPick
    );

    ASSERT_TRUE(
        robot.isMoving()
    );

    EXPECT_TRUE(
        controller.stop()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Idle
    );

    EXPECT_EQ(
        sequence.getState(),
        workcell::CycleState::WaitingForPart
    );

    EXPECT_FALSE(
        robot.isMoving()
    );

    EXPECT_FALSE(
        conveyor.isRunning()
    );
}

TEST_F(
    MachineControllerTest,
    EmergencyStopAbortsActiveSequence
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    robot.setMotionDuration(
        500ms
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    controller.update();

    ASSERT_EQ(
        sequence.getState(),
        workcell::CycleState::MovingToPick
    );

    ASSERT_TRUE(
        controller.emergencyStop()
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::EmergencyStop
    );

    EXPECT_EQ(
        sequence.getState(),
        workcell::CycleState::CycleAborted
    );

    EXPECT_FALSE(
        robot.isMoving()
    );

    EXPECT_FALSE(
        conveyor.isRunning()
    );
}

TEST_F(
    MachineControllerTest,
    PauseTimeDoesNotCauseMotionTimeout
)
{
    robot.setMotionDuration(
        80ms
    );

    sequence.setMotionTimeout(
        150ms
    );

    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    controller.update();

    ASSERT_EQ(
        sequence.getState(),
        workcell::CycleState::MovingToPick
    );

    ASSERT_TRUE(
        controller.pause()
    );

    EXPECT_TRUE(
        sequence.isPaused()
    );

    std::this_thread::sleep_for(
        200ms
    );

    ASSERT_TRUE(
        controller.resume()
    );

    EXPECT_FALSE(
        sequence.isPaused()
    );

    const auto start =
        std::chrono::steady_clock::now();

    while (
        sequence.getState()
            != workcell::CycleState::CycleComplete
        &&
        sequence.getState()
            != workcell::CycleState::CycleFaulted
    )
    {
        controller.update();

        std::this_thread::sleep_for(
            2ms
        );

        ASSERT_LT(
            std::chrono::steady_clock::now()
                - start,
            2s
        );
    }

    EXPECT_EQ(
        sequence.getState(),
        workcell::CycleState::CycleComplete
    );

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Running
    );
}

TEST_F(
    MachineControllerTest,
    RobotCommunicationFailureFaultsMachine
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    robot.setCommunicationFailure(
        true
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    controller.update();
    controller.update();

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    ASSERT_TRUE(
        controller.hasActiveFault()
    );

    EXPECT_EQ(
        controller.getActiveFault()->code,
        workcell::FaultCode::RobotCommunicationLoss
    );
}

TEST_F(
    MachineControllerTest,
    ConveyorStopFailureFaultsMachine
)
{
    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    conveyor.setStopFailure(
        true
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    controller.update();

    EXPECT_EQ(
        sequence.getState(),
        workcell::CycleState::CycleFaulted
    );

    controller.update();

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    ASSERT_TRUE(
        controller.hasActiveFault()
    );

    EXPECT_EQ(
        controller.getActiveFault()->code,
        workcell::FaultCode::ConveyorFailure
    );
}

TEST_F(
    MachineControllerTest,
    GripperFailureFaultsMachine
)
{
    robot.setMotionDuration(
        1ms
    );

    ASSERT_TRUE(
        controller.initialize()
    );

    ASSERT_TRUE(
        controller.start()
    );

    ASSERT_TRUE(
        conveyor.start()
    );

    sensor.setActive(true);

    gripper.setCloseFailure(
        true
    );

    ASSERT_TRUE(
        sequence.startCycle(true)
    );

    const auto start =
        std::chrono::steady_clock::now();

    while (
        controller.getState()
        == workcell::MachineState::Running
    )
    {
        controller.update();

        std::this_thread::sleep_for(
            1ms
        );

        ASSERT_LT(
            std::chrono::steady_clock::now()
                - start,
            1s
        );
    }

    EXPECT_EQ(
        controller.getState(),
        workcell::MachineState::Faulted
    );

    ASSERT_TRUE(
        controller.hasActiveFault()
    );

    EXPECT_EQ(
        controller.getActiveFault()->code,
        workcell::FaultCode::GripperFailure
    );
}

TEST_F(
    MachineControllerTest,
    ProductionCycleRequiresRunningMachine
)
{
    sensor.setActive(true);

    EXPECT_FALSE(
        controller.startProductionCycle(true)
    );

    ASSERT_TRUE(controller.initialize());

    EXPECT_FALSE(
        controller.startProductionCycle(true)
    );
}

TEST_F(
    MachineControllerTest,
    RunningMachineStartsOnlyOneProductionCycle
)
{
    ASSERT_TRUE(controller.initialize());
    ASSERT_TRUE(controller.start());
    ASSERT_TRUE(conveyor.start());
    sensor.setActive(true);

    EXPECT_TRUE(
        controller.startProductionCycle(true)
    );

    EXPECT_FALSE(
        controller.startProductionCycle(false)
    );
}

TEST_F(
    MachineControllerTest,
    CompletedProductionCycleCanStartAgain
)
{
    robot.setMotionDuration(1ms);
    ASSERT_TRUE(controller.initialize());
    ASSERT_TRUE(controller.start());
    ASSERT_TRUE(conveyor.start());
    sensor.setActive(true);
    ASSERT_TRUE(controller.startProductionCycle(true));

    const auto deadline =
        std::chrono::steady_clock::now() + 2s;

    while (
        sequence.getState()
            != workcell::CycleState::CycleComplete
    )
    {
        controller.update();
        std::this_thread::sleep_for(1ms);
        ASSERT_LT(std::chrono::steady_clock::now(), deadline);
    }

    EXPECT_TRUE(controller.startProductionCycle(false));
    EXPECT_EQ(sequence.getTotalCycles(), 1U);
}

TEST_F(MachineControllerTest, SafetyDoorFaultsRunningMachineNaturally)
{
    ASSERT_TRUE(controller.initialize());
    ASSERT_TRUE(controller.start());
    safety.setSafetyDoorOpen(true);

    controller.update();

    EXPECT_EQ(controller.getState(), workcell::MachineState::Faulted);
    ASSERT_TRUE(controller.getActiveFault().has_value());
    EXPECT_EQ(
        controller.getActiveFault()->code,
        workcell::FaultCode::SafetyDoorOpen
    );
}

TEST_F(MachineControllerTest, SafetyDoorMustCloseBeforeRestart)
{
    ASSERT_TRUE(controller.initialize());
    safety.setSafetyDoorOpen(true);
    EXPECT_FALSE(controller.start());
    safety.setSafetyDoorOpen(false);
    EXPECT_TRUE(controller.start());
}
