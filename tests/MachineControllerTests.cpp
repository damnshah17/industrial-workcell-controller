#include <gtest/gtest.h>

#include "machine/MachineController.hpp"
#include "machine/MachineState.hpp"

using workcell::MachineController;
using workcell::MachineState;

TEST(MachineControllerTest, StartsOffline)
{
    MachineController controller;

    EXPECT_EQ(
        controller.getState(),
        MachineState::Offline
    );
}

TEST(MachineControllerTest, InitializeMovesMachineToIdle)
{
    MachineController controller;

    EXPECT_TRUE(controller.initialize());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Idle
    );
}

TEST(MachineControllerTest, CannotStartWhileOffline)
{
    MachineController controller;

    EXPECT_FALSE(controller.start());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Offline
    );
}

TEST(MachineControllerTest, StartFromIdleMovesToRunning)
{
    MachineController controller;

    controller.initialize();

    EXPECT_TRUE(controller.start());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Running
    );
}

TEST(MachineControllerTest, RunningMachineCanPause)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    EXPECT_TRUE(controller.pause());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Paused
    );
}

TEST(MachineControllerTest, PausedMachineCanResume)
{
    MachineController controller;

    controller.initialize();
    controller.start();
    controller.pause();

    EXPECT_TRUE(controller.resume());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Running
    );
}

TEST(MachineControllerTest, RunningMachineCanStop)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    EXPECT_TRUE(controller.stop());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Idle
    );
}

TEST(MachineControllerTest, CannotPauseIdleMachine)
{
    MachineController controller;

    controller.initialize();

    EXPECT_FALSE(controller.pause());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Idle
    );
}

TEST(MachineControllerTest, CannotInitializeRunningMachine)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    EXPECT_FALSE(controller.initialize());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Running
    );
}

TEST(
    MachineControllerTest,
    EmergencyStopInterruptsRunningMachine
)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    EXPECT_TRUE(controller.emergencyStop());

    EXPECT_EQ(
        controller.getState(),
        MachineState::EmergencyStop
    );

    EXPECT_TRUE(
        controller.isEmergencyStopActive()
    );
}

TEST(
    MachineControllerTest,
    EmergencyStopCannotResetWhileStillActive
)
{
    MachineController controller;

    controller.initialize();
    controller.start();
    controller.emergencyStop();

    EXPECT_FALSE(controller.reset());

    EXPECT_EQ(
        controller.getState(),
        MachineState::EmergencyStop
    );
}

TEST(
    MachineControllerTest,
    EmergencyStopCanResetAfterConditionCleared
)
{
    MachineController controller;

    controller.initialize();
    controller.start();
    controller.emergencyStop();

    EXPECT_TRUE(
        controller.clearEmergencyStop()
    );

    EXPECT_TRUE(
        controller.reset()
    );

    EXPECT_EQ(
        controller.getState(),
        MachineState::Idle
    );
}

TEST(
    MachineControllerTest,
    FaultMovesRunningMachineToFaulted
)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    EXPECT_TRUE(
        controller.triggerFault(
            workcell::FaultCode::MotionTimeout,
            "Robot failed to reach position"
        )
    );

    EXPECT_EQ(
        controller.getState(),
        MachineState::Faulted
    );

    EXPECT_TRUE(
        controller.hasActiveFault()
    );
}

TEST(
    MachineControllerTest,
    FaultedMachineCannotStart
)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    controller.triggerFault(
        workcell::FaultCode::MotionTimeout,
        "Robot motion timeout"
    );

    EXPECT_FALSE(controller.start());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Faulted
    );
}

TEST(
    MachineControllerTest,
    ResetClearsFaultAndReturnsMachineToIdle
)
{
    MachineController controller;

    controller.initialize();
    controller.start();

    controller.triggerFault(
        workcell::FaultCode::MotionTimeout,
        "Robot motion timeout"
    );

    EXPECT_TRUE(controller.reset());

    EXPECT_EQ(
        controller.getState(),
        MachineState::Idle
    );

    EXPECT_FALSE(
        controller.hasActiveFault()
    );
}