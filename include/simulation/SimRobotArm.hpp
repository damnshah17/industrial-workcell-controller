#pragma once

#include "hardware/IRobotArm.hpp"

namespace workcell {

class SimRobotArm : public IRobotArm
{
public:
    SimRobotArm();

    bool initialize() override;

    bool moveTo(RobotPosition position) override;

    bool stop() override;

    RobotPosition getPosition() const override;

    bool isMoving() const override;

    bool isInitialized() const override;

private:
    bool initialized_;
    bool moving_;
    RobotPosition position_;
};

}