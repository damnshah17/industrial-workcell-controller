#include "machine/MachineController.hpp"

#include "logging/Logger.hpp"

namespace workcell {

MachineController::MachineController(
    SequenceController& sequenceController,
    SafetyController& safetyController,
    FaultManager& faultManager
)
    : currentState_(MachineState::Offline),
      sequenceController_(sequenceController),
      safetyController_(safetyController),
      faultManager_(faultManager)
{
}

MachineState MachineController::getState() const
{
    return currentState_;
}

bool MachineController::initialize()
{
    if (!transitionTo(
            MachineState::Initializing
        ))
    {
        return false;
    }

    Logger::info(
        "Machine initialization started."
    );

    Logger::info(
        "Machine initialization completed."
    );

    return transitionTo(
        MachineState::Idle
    );
}

bool MachineController::start()
{
    if (safetyController_.isSafetyDoorOpen())
    {
        Logger::safety(
            "Start rejected because the safety door is open."
        );

        return false;
    }

    if (
        safetyController_
            .isEmergencyStopActive()
    )
    {
        Logger::safety(
            "Start rejected because Emergency Stop is active."
        );

        return false;
    }

    if (
        faultManager_
            .hasActiveFault()
    )
    {
        Logger::error(
            "Start rejected because an active fault exists."
        );

        return false;
    }

    return transitionTo(
        MachineState::Running
    );
}

bool MachineController::startProductionCycle(
    bool inspectionAccepted
)
{
    if (currentState_ != MachineState::Running)
    {
        Logger::warning(
            "Cycle start rejected because machine is not Running."
        );

        return false;
    }

    if (
        sequenceController_.getState()
        == CycleState::CycleComplete
    )
    {
        sequenceController_.resetForNextCycle();
    }

    return sequenceController_.startCycle(
        inspectionAccepted
    );
}

bool MachineController::startProductionCycle(const std::string& sampleId)
{
    if (currentState_ != MachineState::Running)
    {
        return false;
    }

    if (sequenceController_.getState() == CycleState::CycleComplete)
    {
        sequenceController_.resetForNextCycle();
    }

    return sequenceController_.startCycle(sampleId);
}

bool MachineController::pause()
{
    if (
        currentState_
        != MachineState::Running
    )
    {
        return false;
    }

    sequenceController_.pause();

    return transitionTo(
        MachineState::Paused
    );
}

bool MachineController::resume()
{
    if (
        safetyController_
            .isEmergencyStopActive()
    )
    {
        return false;
    }

    if (
        faultManager_
            .hasActiveFault()
    )
    {
        return false;
    }

    if (
        currentState_
        != MachineState::Paused
    )
    {
        return false;
    }

    if (
        sequenceController_.isPaused()
    )
    {
        sequenceController_.resume();
    }

    return transitionTo(
        MachineState::Running
    );
}

bool MachineController::stop()
{
    if (!transitionTo(
            MachineState::Stopping
        ))
    {
        return false;
    }

    sequenceController_.abort();

    if (
        sequenceController_.getState()
        == CycleState::CycleAborted
        ||
        sequenceController_.getState()
        == CycleState::CycleComplete
        ||
        sequenceController_.getState()
        == CycleState::CycleFaulted
    )
    {
        sequenceController_
            .resetForNextCycle();
    }

    Logger::info(
        "Machine stopping."
    );

    return transitionTo(
        MachineState::Idle
    );
}

bool MachineController::reset()
{
    if (
        currentState_
        == MachineState::Faulted
    )
    {
        faultManager_.clearFault();

        sequenceController_
            .resetForNextCycle();

        return transitionTo(
            MachineState::Idle
        );
    }

    if (
        currentState_
        == MachineState::EmergencyStop
    )
    {
        if (
            safetyController_
                .isEmergencyStopActive()
        )
        {
            Logger::safety(
                "Reset rejected because Emergency Stop remains active."
            );

            return false;
        }

        sequenceController_
            .resetForNextCycle();

        return transitionTo(
            MachineState::Idle
        );
    }

    return false;
}

bool MachineController::emergencyStop()
{
    if (
        !safetyController_
            .activateEmergencyStop()
    )
    {
        return false;
    }

    sequenceController_.abort();

    return transitionTo(
        MachineState::EmergencyStop
    );
}

bool MachineController::clearEmergencyStop()
{
    return safetyController_
        .clearEmergencyStop();
}

void MachineController::update()
{
    if (
        currentState_
        != MachineState::Running
    )
    {
        return;
    }

    if (safetyController_.isSafetyDoorOpen())
    {
        triggerFault(
            FaultCode::SafetyDoorOpen,
            "Safety door opened while the machine was running."
        );

        return;
    }

    sequenceController_.update();

    if (
        sequenceController_.getState()
        == CycleState::CycleFaulted
    )
    {
        const auto& sequenceFault =
            sequenceController_.getFault();

        if (sequenceFault.has_value())
        {
            faultManager_.raiseFault(
                sequenceFault->code,
                sequenceFault->message
            );
        }
        else
        {
            faultManager_.raiseFault(
                FaultCode::InitializationFailure,
                "Sequence entered fault state without fault details."
            );
        }

        transitionTo(
            MachineState::Faulted
        );
    }
}

bool MachineController::hasActiveFault() const
{
    return faultManager_
        .hasActiveFault();
}

const std::optional<Fault>&
MachineController::getActiveFault() const
{
    return faultManager_
        .getActiveFault();
}

bool MachineController::isEmergencyStopActive() const
{
    return safetyController_
        .isEmergencyStopActive();
}

bool MachineController::transitionTo(
    MachineState targetState
)
{
    if (
        !isValidTransition(
            currentState_,
            targetState
        )
    )
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

    currentState_ =
        targetState;

    return true;
}

bool MachineController::isValidTransition(
    MachineState from,
    MachineState to
) const
{
    if (
        to == MachineState::EmergencyStop
        &&
        from != MachineState::EmergencyStop
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

bool MachineController::triggerFault(
    FaultCode code,
    const std::string& message
)
{
    if (
        currentState_
        == MachineState::EmergencyStop
    )
    {
        Logger::warning(
            "Fault injection rejected while Emergency Stop is active."
        );

        return false;
    }

    if (
        currentState_
        == MachineState::Faulted
    )
    {
        Logger::warning(
            "Fault injection rejected because machine is already faulted."
        );

        return false;
    }

    if (
        !isValidTransition(
            currentState_,
            MachineState::Faulted
        )
    )
    {
        Logger::warning(
            "Fault injection rejected from current machine state."
        );

        return false;
    }

    if (
        !faultManager_.raiseFault(
            code,
            message
        )
    )
    {
        return false;
    }

    sequenceController_.abort();

    return transitionTo(
        MachineState::Faulted
    );
}

} // namespace workcell
