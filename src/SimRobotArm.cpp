#include "simulation/SimRobotArm.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SimRobotArm::SimRobotArm()
    : initialized_(false),
      moving_(false),
      position_(RobotPosition::Home)
{
}

bool SimRobotArm::initialize()
{
    initialized_ = true;
    moving_ = false;
    position_ = RobotPosition::Home;

    Logger::info("SimRobotArm initialized at Home.");

    return true;
}

bool SimRobotArm::moveTo(RobotPosition position)
{
    if (!initialized_)
    {
        Logger::error(
            "Robot move rejected because robot is not initialized."
        );

        return false;
    }

    moving_ = true;

    Logger::info(
        "Robot moving from "
        + toString(position_)
        + " to "
        + toString(position)
    );

    // Phase 2A simulation completes movement immediately.
    position_ = position;
    moving_ = false;

    Logger::info(
        "Robot reached "
        + toString(position_)
    );

    return true;
}

bool SimRobotArm::stop()
{
    if (!initialized_)
    {
        return false;
    }

    moving_ = false;

    Logger::info("Robot stopped.");

    return true;
}

RobotPosition SimRobotArm::getPosition() const
{
    return position_;
}

bool SimRobotArm::isMoving() const
{
    return moving_;
}

bool SimRobotArm::isInitialized() const
{
    return initialized_;
}

}