#pragma once

#include "hardware/IRobotArm.hpp"

#include <chrono>

namespace workcell {

class SimRobotArm : public IRobotArm
{
public:
    explicit SimRobotArm(
        std::chrono::milliseconds motionDuration =
            std::chrono::milliseconds(500)
    );

    bool initialize() override;

    bool moveTo(RobotPosition position) override;

    bool stop() override;

    RobotPosition getPosition() const override;

    bool isMoving() const override;

    bool isInitialized() const override;

    void update() override;

    void setMotionDuration(
        std::chrono::milliseconds duration
    );

private:
    bool initialized_;
    bool moving_;

    RobotPosition position_;
    RobotPosition targetPosition_;

    std::chrono::milliseconds motionDuration_;
    std::chrono::steady_clock::time_point motionStart_;
};

} // namespace workcell