#include "simulation/SimRobotArm.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SimRobotArm::SimRobotArm(
    std::chrono::milliseconds motionDuration
)
    : initialized_(false),
      moving_(false),
      communicationFailure_(false),
      position_(RobotPosition::Home),
      targetPosition_(RobotPosition::Home),
      motionDuration_(motionDuration)
{
}

bool SimRobotArm::initialize()
{
    initialized_ = true;
    moving_ = false;

    position_ =
        RobotPosition::Home;

    targetPosition_ =
        RobotPosition::Home;

    communicationFailure_ = false;

    Logger::info(
        "SimRobotArm initialized at Home."
    );

    return true;
}

bool SimRobotArm::moveTo(
    RobotPosition position
)
{
    if (!initialized_)
    {
        Logger::error(
            "Robot move rejected because robot is not initialized."
        );

        return false;
    }

    if (communicationFailure_)
    {
        Logger::error(
            "Robot move failed because communication failure is active."
        );

        return false;
    }

    if (moving_)
    {
        Logger::warning(
            "Robot move rejected because another motion is active."
        );

        return false;
    }

    targetPosition_ = position;
    moving_ = true;

    motionStart_ =
        std::chrono::steady_clock::now();

    Logger::info(
        "Robot motion started: "
        + toString(position_)
        + " -> "
        + toString(targetPosition_)
    );

    return true;
}

void SimRobotArm::update()
{
    if (!moving_)
    {
        return;
    }

    if (communicationFailure_)
    {
        Logger::error(
            "Robot communication lost during motion."
        );

        moving_ = false;

        return;
    }

    const auto now =
        std::chrono::steady_clock::now();

    const auto elapsed =
        std::chrono::duration_cast<
            std::chrono::milliseconds
        >(
            now - motionStart_
        );

    if (
        elapsed >= motionDuration_
    )
    {
        position_ =
            targetPosition_;

        moving_ = false;

        Logger::info(
            "Robot reached "
            + toString(position_)
        );
    }
}

bool SimRobotArm::stop()
{
    if (!initialized_)
    {
        return false;
    }

    moving_ = false;

    Logger::info(
        "Robot motion stopped."
    );

    return true;
}

RobotPosition
SimRobotArm::getPosition() const
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

void SimRobotArm::setMotionDuration(
    std::chrono::milliseconds duration
)
{
    motionDuration_ = duration;
}

void SimRobotArm::setCommunicationFailure(
    bool enabled
)
{
    communicationFailure_ = enabled;

    if (enabled)
    {
        Logger::warning(
            "Simulated robot communication failure enabled."
        );
    }
    else
    {
        Logger::info(
            "Simulated robot communication failure cleared."
        );
    }
}

bool SimRobotArm::hasCommunicationFailure() const
{
    return communicationFailure_;
}

} // namespace workcell