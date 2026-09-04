#pragma once

#include "hardware/IConveyor.hpp"
#include "hardware/IRobotArm.hpp"

namespace workcell {

class SafetyController
{
public:
    SafetyController(
        IRobotArm& robot,
        IConveyor& conveyor
    );

    bool activateEmergencyStop();

    bool clearEmergencyStop();

    bool isEmergencyStopActive() const;

    void setSafetyDoorOpen(bool open);

    bool isSafetyDoorOpen() const;

private:
    IRobotArm& robot_;
    IConveyor& conveyor_;

    bool emergencyStopActive_;
    bool safetyDoorOpen_;
};

} // namespace workcell
