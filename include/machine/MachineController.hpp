#pragma once

#include "faults/Fault.hpp"
#include "machine/MachineState.hpp"

#include <optional>
#include <string>

namespace workcell {

class MachineController
{
public:
    MachineController();

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

    bool hasActiveFault() const;

    const std::optional<Fault>& getActiveFault() const;

    bool isEmergencyStopActive() const;

private:
    MachineState currentState_;

    bool emergencyStopActive_;

    std::optional<Fault> activeFault_;

    bool transitionTo(MachineState targetState);

    bool isValidTransition(
        MachineState from,
        MachineState to
    ) const;
};

} // namespace workcell