#include "simulation/SimGripper.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SimGripper::SimGripper()
    : initialized_(false),
      open_(true)
{
}

bool SimGripper::initialize()
{
    initialized_ = true;
    open_ = true;

    Logger::info("SimGripper initialized and open.");

    return true;
}

bool SimGripper::open()
{
    if (!initialized_)
    {
        Logger::error(
            "Gripper open rejected because gripper is not initialized."
        );

        return false;
    }

    open_ = true;

    Logger::info("Gripper opened.");

    return true;
}

bool SimGripper::close()
{
    if (!initialized_)
    {
        Logger::error(
            "Gripper close rejected because gripper is not initialized."
        );

        return false;
    }

    open_ = false;

    Logger::info("Gripper closed.");

    return true;
}

bool SimGripper::isOpen() const
{
    return open_;
}

bool SimGripper::isInitialized() const
{
    return initialized_;
}

}