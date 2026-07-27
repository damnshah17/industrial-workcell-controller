#include "simulation/SimConveyor.hpp"

#include "logging/Logger.hpp"

namespace workcell {

SimConveyor::SimConveyor()
    : initialized_(false),
      running_(false)
{
}

bool SimConveyor::initialize()
{
    initialized_ = true;
    running_ = false;

    Logger::info("SimConveyor initialized.");

    return true;
}

bool SimConveyor::start()
{
    if (!initialized_)
    {
        Logger::error(
            "Conveyor start rejected because conveyor is not initialized."
        );

        return false;
    }

    running_ = true;

    Logger::info("Conveyor started.");

    return true;
}

bool SimConveyor::stop()
{
    if (!initialized_)
    {
        Logger::error(
            "Conveyor stop rejected because conveyor is not initialized."
        );

        return false;
    }

    running_ = false;

    Logger::info("Conveyor stopped.");

    return true;
}

bool SimConveyor::isRunning() const
{
    return running_;
}

bool SimConveyor::isInitialized() const
{
    return initialized_;
}

}