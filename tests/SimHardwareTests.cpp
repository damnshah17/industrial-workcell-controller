#include <gtest/gtest.h>

#include "hardware/RobotPosition.hpp"
#include "simulation/SimConveyor.hpp"
#include "simulation/SimGripper.hpp"
#include "simulation/SimPartSensor.hpp"
#include "simulation/SimRobotArm.hpp"

#include <chrono>
#include <thread>

using workcell::RobotPosition;
using workcell::SimConveyor;
using workcell::SimGripper;
using workcell::SimPartSensor;
using workcell::SimRobotArm;

TEST(SimRobotArmTest, StartsUninitialized)
{
    SimRobotArm robot;

    EXPECT_FALSE(robot.isInitialized());
}

TEST(SimRobotArmTest, InitializePlacesRobotAtHome)
{
    SimRobotArm robot;

    EXPECT_TRUE(robot.initialize());

    EXPECT_TRUE(robot.isInitialized());

    EXPECT_EQ(
        robot.getPosition(),
        RobotPosition::Home
    );
}

TEST(SimRobotArmTest, CannotMoveBeforeInitialization)
{
    SimRobotArm robot;

    EXPECT_FALSE(
        robot.moveTo(RobotPosition::Pick)
    );
}

TEST(SimRobotArmTest, CanMoveAfterInitialization)
{
    using namespace std::chrono_literals;

    SimRobotArm robot(1ms);

    robot.initialize();

    EXPECT_TRUE(
        robot.moveTo(RobotPosition::Pick)
    );

    EXPECT_TRUE(
        robot.isMoving()
    );

    std::this_thread::sleep_for(2ms);

    robot.update();

    EXPECT_FALSE(
        robot.isMoving()
    );

    EXPECT_EQ(
        robot.getPosition(),
        RobotPosition::Pick
    );
}

TEST(SimConveyorTest, StartsStopped)
{
    SimConveyor conveyor;

    EXPECT_FALSE(conveyor.isRunning());
}

TEST(SimConveyorTest, CanStartAfterInitialization)
{
    SimConveyor conveyor;

    conveyor.initialize();

    EXPECT_TRUE(conveyor.start());

    EXPECT_TRUE(conveyor.isRunning());
}

TEST(SimConveyorTest, CanStop)
{
    SimConveyor conveyor;

    conveyor.initialize();
    conveyor.start();

    EXPECT_TRUE(conveyor.stop());

    EXPECT_FALSE(conveyor.isRunning());
}

TEST(SimGripperTest, InitializesOpen)
{
    SimGripper gripper;

    gripper.initialize();

    EXPECT_TRUE(gripper.isOpen());
}

TEST(SimGripperTest, CanCloseAndOpen)
{
    SimGripper gripper;

    gripper.initialize();

    EXPECT_TRUE(gripper.close());
    EXPECT_FALSE(gripper.isOpen());

    EXPECT_TRUE(gripper.open());
    EXPECT_TRUE(gripper.isOpen());
}

TEST(SimPartSensorTest, CanSimulatePartDetection)
{
    SimPartSensor sensor;

    sensor.initialize();

    EXPECT_FALSE(sensor.isActive());

    sensor.setActive(true);

    EXPECT_TRUE(sensor.isActive());

    sensor.setActive(false);

    EXPECT_FALSE(sensor.isActive());
}