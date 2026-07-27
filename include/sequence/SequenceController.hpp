#pragma once

#include "hardware/IConveyor.hpp"
#include "hardware/IGripper.hpp"
#include "hardware/IRobotArm.hpp"
#include "hardware/ISensor.hpp"
#include "sequence/CycleState.hpp"
#include "faults/Fault.hpp"

#include <chrono>
#include <optional>


namespace workcell {

class SequenceController
{
public:
    SequenceController(
        IRobotArm& robot,
        IConveyor& conveyor,
        IGripper& gripper,
        ISensor& partSensor
    );

    CycleState getState() const;

    bool startCycle(
        bool inspectionAccepted
    );

    void update();

    void abort();

    bool resetForNextCycle();

    const std::optional<Fault>& getFault() const;

    unsigned int getTotalCycles() const;

    unsigned int getAcceptedCycles() const;

    unsigned int getRejectedCycles() const;

    void setMotionTimeout(
        std::chrono::milliseconds timeout
    );

private:
    IRobotArm& robot_;
    IConveyor& conveyor_;
    IGripper& gripper_;
    ISensor& partSensor_;

    CycleState currentState_;

    unsigned int totalCycles_;
    unsigned int acceptedCycles_;
    unsigned int rejectedCycles_;

    std::optional<Fault> fault_;

    std::optional<bool> inspectionAccepted_;

    std::chrono::milliseconds motionTimeout_;

    std::chrono::steady_clock::time_point
        stateStartTime_;

    void transitionTo(
        CycleState state
    );

    bool verifyDevicesReady() const;

    bool hasStateTimedOut() const;

    void failCycle(
        FaultCode code,
        const char* reason
    );
};

} // namespace workcell