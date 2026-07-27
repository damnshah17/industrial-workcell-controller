#include "simulation/SimPartSensor.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SimPartSensor::SimPartSensor()
    : initialized_(false),
      active_(false)
{
}

bool SimPartSensor::initialize()
{
    initialized_ = true;
    active_ = false;

    Logger::info("SimPartSensor initialized.");

    return true;
}

bool SimPartSensor::isActive() const
{
    return active_;
}

bool SimPartSensor::isInitialized() const
{
    return initialized_;
}

void SimPartSensor::setActive(bool active)
{
    if (!initialized_)
    {
        Logger::warning(
            "Part sensor state changed before initialization."
        );
    }

    active_ = active;

    Logger::info(
        active_
            ? "Part sensor ACTIVE."
            : "Part sensor CLEAR."
    );
}

}