#pragma once

#include "faults/Fault.hpp"
#include "faults/FaultManager.hpp"
#include "machine/MachineState.hpp"
#include "safety/SafetyController.hpp"
#include "sequence/SequenceController.hpp"

#include <optional>
#include <string>

namespace workcell {

class MachineController
{
public:
    MachineController(
        SequenceController& sequenceController,
        SafetyController& safetyController,
        FaultManager& faultManager
    );

    MachineState getState() const;

    bool initialize();

    bool start();

    bool pause();

    bool resume();

    bool stop();

    bool reset();

    bool emergencyStop();

    bool clearEmergencyStop();

    bool triggerFault(
        FaultCode code,
        const std::string& message
    );

    void update();

    bool hasActiveFault() const;

    const std::optional<Fault>&
    getActiveFault() const;

    bool isEmergencyStopActive() const;

private:
    MachineState currentState_;

    SequenceController& sequenceController_;
    SafetyController& safetyController_;
    FaultManager& faultManager_;

    bool transitionTo(
        MachineState targetState
    );

    bool isValidTransition(
        MachineState from,
        MachineState to
    ) const;
};

} // namespace workcell