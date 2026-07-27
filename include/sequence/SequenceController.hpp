#pragma once

#include "hardware/IConveyor.hpp"
#include "hardware/IGripper.hpp"
#include "hardware/IRobotArm.hpp"
#include "hardware/ISensor.hpp"
#include "sequence/CycleState.hpp"

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

    bool runCycle(bool inspectionAccepted);

    bool resetForNextCycle();

    unsigned int getTotalCycles() const;

    unsigned int getAcceptedCycles() const;

    unsigned int getRejectedCycles() const;

private:
    IRobotArm& robot_;
    IConveyor& conveyor_;
    IGripper& gripper_;
    ISensor& partSensor_;

    CycleState currentState_;

    unsigned int totalCycles_;
    unsigned int acceptedCycles_;
    unsigned int rejectedCycles_;

    void transitionTo(CycleState state);

    bool verifyDevicesReady() const;

    bool failCycle(const char* reason);
};

} // namespace workcell