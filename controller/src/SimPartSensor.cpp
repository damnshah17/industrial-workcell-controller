#include "simulation/SimPartSensor.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SimPartSensor::SimPartSensor()
    : initialized_(false),
      active_(false),
      failure_(false)
{
}

bool SimPartSensor::initialize()
{
    initialized_ = true;
    active_ = false;
    failure_ = false;

    Logger::info("SimPartSensor initialized.");

    return true;
}

bool SimPartSensor::isActive() const
{
    return active_ && !failure_;
}

bool SimPartSensor::isInitialized() const
{
    return initialized_;
}

bool SimPartSensor::isHealthy() const
{
    return initialized_ && !failure_;
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

void SimPartSensor::setFailure(bool enabled)
{
    failure_ = enabled;

    Logger::warning(
        enabled
            ? "Simulated part sensor failure enabled."
            : "Simulated part sensor failure cleared."
    );
}

}
