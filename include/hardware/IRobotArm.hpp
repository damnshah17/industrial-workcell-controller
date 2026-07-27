#pragma once

#include "hardware/RobotPosition.hpp"

namespace workcell {

class IRobotArm
{
public:
    virtual ~IRobotArm() = default;

    virtual bool initialize() = 0;

    virtual bool moveTo(
        RobotPosition position
    ) = 0;

    virtual bool stop() = 0;

    virtual void update() = 0;

    virtual RobotPosition getPosition() const = 0;

    virtual bool isMoving() const = 0;

    virtual bool isInitialized() const = 0;
};

} // namespace workcell