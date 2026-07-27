#include "machine/MachineController.hpp"

#include "logging/Logger.hpp"

namespace workcell {

MachineController::MachineController()
    : currentState_(MachineState::Offline),
      emergencyStopActive_(false),
      activeFault_(std::nullopt)
{
}

MachineState MachineController::getState() const
{
    return currentState_;
}

bool MachineController::initialize()
{
    if (!transitionTo(MachineState::Initializing))
    {
        return false;
    }

    Logger::info("Machine initialization started.");

    // Phase 2 will initialize real/simulated hardware here.

    Logger::info("Machine initialization completed.");

    return transitionTo(MachineState::Idle);
}

bool MachineController::start()
{
    if (emergencyStopActive_)
    {
        Logger::safety(
            "Start rejected because Emergency Stop is active."
        );

        return false;
    }

    if (activeFault_.has_value())
    {
        Logger::error(
            "Start rejected because an active fault exists."
        );

        return false;
    }

    return transitionTo(MachineState::Running);
}

bool MachineController::pause()
{
    return transitionTo(MachineState::Paused);
}

bool MachineController::resume()
{
    if (emergencyStopActive_)
    {
        Logger::safety(
            "Resume rejected because Emergency Stop is active."
        );

        return false;
    }

    if (activeFault_.has_value())
    {
        Logger::error(
            "Resume rejected because an active fault exists."
        );

        return false;
    }

    return transitionTo(MachineState::Running);
}

bool MachineController::stop()
{
    if (!transitionTo(MachineState::Stopping))
    {
        return false;
    }

    Logger::info(
        "Machine stopping and moving toward safe idle state."
    );

    // Phase 2 will stop equipment safely here.

    return transitionTo(MachineState::Idle);
}

bool MachineController::reset()
{
    if (currentState_ == MachineState::Faulted)
    {
        if (!activeFault_.has_value())
        {
            Logger::warning(
                "Machine is Faulted but no active fault exists."
            );
        }

        activeFault_.reset();

        Logger::info("Active fault cleared.");

        return transitionTo(MachineState::Idle);
    }

    if (currentState_ == MachineState::EmergencyStop)
    {
        if (emergencyStopActive_)
        {
            Logger::safety(
                "Reset rejected. Emergency Stop must be cleared first."
            );

            return false;
        }

        Logger::info(
            "Emergency Stop condition cleared. Resetting machine."
        );

        return transitionTo(MachineState::Idle);
    }

    Logger::warning(
        "Reset rejected because machine is not Faulted "
        "or EmergencyStop."
    );

    return false;
}

bool MachineController::emergencyStop()
{
    if (currentState_ == MachineState::EmergencyStop)
    {
        Logger::warning(
            "Emergency Stop is already active."
        );

        return false;
    }

    emergencyStopActive_ = true;

    Logger::safety("Emergency Stop activated.");

    return transitionTo(MachineState::EmergencyStop);
}

bool MachineController::clearEmergencyStop()
{
    if (!emergencyStopActive_)
    {
        Logger::warning(
            "Emergency Stop is not currently active."
        );

        return false;
    }

    emergencyStopActive_ = false;

    Logger::safety(
        "Emergency Stop physical condition cleared. "
        "Machine reset is still required."
    );

    return true;
}

bool MachineController::triggerFault(
    FaultCode code,
    const std::string& message
)
{
    if (
        currentState_ == MachineState::Offline
        || currentState_ == MachineState::EmergencyStop
    )
    {
        Logger::error(
            "Fault cannot be entered from current machine state."
        );

        return false;
    }

    activeFault_ = Fault{
        code,
        message
    };

    Logger::error(
        "Fault raised: "
        + toString(code)
        + " - "
        + message
    );

    if (!transitionTo(MachineState::Faulted))
    {
        activeFault_.reset();

        return false;
    }

    return true;
}

bool MachineController::hasActiveFault() const
{
    return activeFault_.has_value();
}

const std::optional<Fault>&
MachineController::getActiveFault() const
{
    return activeFault_;
}

bool MachineController::isEmergencyStopActive() const
{
    return emergencyStopActive_;
}

bool MachineController::transitionTo(
    MachineState targetState
)
{
    if (!isValidTransition(currentState_, targetState))
    {
        Logger::error(
            "Invalid state transition: "
            + toString(currentState_)
            + " -> "
            + toString(targetState)
        );

        return false;
    }

    Logger::state(
        toString(currentState_)
        + " -> "
        + toString(targetState)
    );

    currentState_ = targetState;

    return true;
}

bool MachineController::isValidTransition(
    MachineState from,
    MachineState to
) const
{
    // Emergency Stop takes priority over normal machine operation.
    if (
        to == MachineState::EmergencyStop
        && from != MachineState::EmergencyStop
    )
    {
        return true;
    }

    switch (from)
    {
        case MachineState::Offline:
            return to == MachineState::Initializing;

        case MachineState::Initializing:
            return to == MachineState::Idle
                || to == MachineState::Faulted;

        case MachineState::Idle:
            return to == MachineState::Running
                || to == MachineState::Faulted;

        case MachineState::Running:
            return to == MachineState::Paused
                || to == MachineState::Stopping
                || to == MachineState::Faulted;

        case MachineState::Paused:
            return to == MachineState::Running
                || to == MachineState::Stopping
                || to == MachineState::Faulted;

        case MachineState::Stopping:
            return to == MachineState::Idle
                || to == MachineState::Faulted;

        case MachineState::Faulted:
            return to == MachineState::Idle;

        case MachineState::EmergencyStop:
            return to == MachineState::Idle;
    }

    return false;
}

}