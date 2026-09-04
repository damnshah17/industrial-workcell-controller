#include "safety/SafetyController.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SafetyController::SafetyController(
    IRobotArm& robot,
    IConveyor& conveyor
)
    : robot_(robot),
      conveyor_(conveyor),
      emergencyStopActive_(false),
      safetyDoorOpen_(false)
{
}

bool SafetyController::activateEmergencyStop()
{
    if (emergencyStopActive_)
    {
        Logger::warning(
            "Emergency Stop is already active."
        );

        return false;
    }

    emergencyStopActive_ = true;

    Logger::safety(
        "Emergency Stop activated."
    );

    robot_.stop();
    conveyor_.stop();

    Logger::safety(
        "Robot and conveyor commanded to stop."
    );

    return true;
}

bool SafetyController::clearEmergencyStop()
{
    if (!emergencyStopActive_)
    {
        Logger::warning(
            "Emergency Stop is not active."
        );

        return false;
    }

    emergencyStopActive_ = false;

    Logger::safety(
        "Emergency Stop condition cleared."
    );

    return true;
}

bool SafetyController::isEmergencyStopActive() const
{
    return emergencyStopActive_;
}

void SafetyController::setSafetyDoorOpen(bool open)
{
    safetyDoorOpen_ = open;

    Logger::safety(
        open
            ? "Safety door opened."
            : "Safety door closed."
    );
}

bool SafetyController::isSafetyDoorOpen() const
{
    return safetyDoorOpen_;
}

} // namespace workcell
