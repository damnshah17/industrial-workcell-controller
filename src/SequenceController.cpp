#include "sequence/SequenceController.hpp"

#include "hardware/RobotPosition.hpp"
#include "logging/Logger.hpp"

#include <string>

namespace workcell {

SequenceController::SequenceController(
    IRobotArm& robot,
    IConveyor& conveyor,
    IGripper& gripper,
    ISensor& partSensor
)
    : robot_(robot),
      conveyor_(conveyor),
      gripper_(gripper),
      partSensor_(partSensor),
      currentState_(CycleState::WaitingForPart),
      totalCycles_(0),
      acceptedCycles_(0),
      rejectedCycles_(0)
{
}

CycleState SequenceController::getState() const
{
    return currentState_;
}

bool SequenceController::runCycle(
    bool inspectionAccepted
)
{
    if (!verifyDevicesReady())
    {
        return failCycle(
            "Production cycle rejected because one or more devices are not initialized."
        );
    }

    if (currentState_ != CycleState::WaitingForPart)
    {
        return failCycle(
            "Production cycle can only start from WaitingForPart."
        );
    }

    if (!partSensor_.isActive())
    {
        Logger::info(
            "No part detected. Sequence remains in WaitingForPart."
        );

        return false;
    }

    Logger::info("Part detected. Starting production cycle.");

    // ==========================================
    // Stop conveyor
    // ==========================================

    transitionTo(CycleState::StoppingConveyor);

    if (!conveyor_.stop())
    {
        return failCycle("Failed to stop conveyor.");
    }

    // ==========================================
    // Move robot to pick position
    // ==========================================

    transitionTo(CycleState::MovingToPick);

    if (!robot_.moveTo(RobotPosition::Pick))
    {
        return failCycle(
            "Robot failed to reach Pick position."
        );
    }

    // ==========================================
    // Pick part
    // ==========================================

    transitionTo(CycleState::ClosingGripper);

    if (!gripper_.close())
    {
        return failCycle(
            "Gripper failed to close."
        );
    }

    // ==========================================
    // Move to inspection
    // ==========================================

    transitionTo(CycleState::MovingToInspection);

    if (!robot_.moveTo(RobotPosition::Inspect))
    {
        return failCycle(
            "Robot failed to reach Inspect position."
        );
    }

    // ==========================================
    // Inspection
    // ==========================================

    transitionTo(CycleState::Inspecting);

    Logger::info(
        inspectionAccepted
            ? "Inspection result: ACCEPTED."
            : "Inspection result: REJECTED."
    );

    // ==========================================
    // Route part
    // ==========================================

    if (inspectionAccepted)
    {
        transitionTo(
            CycleState::MovingToAcceptBin
        );

        if (!robot_.moveTo(
                RobotPosition::AcceptBin
            ))
        {
            return failCycle(
                "Robot failed to reach AcceptBin."
            );
        }
    }
    else
    {
        transitionTo(
            CycleState::MovingToRejectBin
        );

        if (!robot_.moveTo(
                RobotPosition::RejectBin
            ))
        {
            return failCycle(
                "Robot failed to reach RejectBin."
            );
        }
    }

    // ==========================================
    // Release part
    // ==========================================

    transitionTo(CycleState::ReleasingPart);

    if (!gripper_.open())
    {
        return failCycle(
            "Gripper failed to release part."
        );
    }

    // ==========================================
    // Return robot home
    // ==========================================

    transitionTo(CycleState::ReturningHome);

    if (!robot_.moveTo(RobotPosition::Home))
    {
        return failCycle(
            "Robot failed to return Home."
        );
    }

    // ==========================================
    // Restart conveyor
    // ==========================================

    transitionTo(
        CycleState::RestartingConveyor
    );

    if (!conveyor_.start())
    {
        return failCycle(
            "Failed to restart conveyor."
        );
    }

    // ==========================================
    // Complete cycle
    // ==========================================

    ++totalCycles_;

    if (inspectionAccepted)
    {
        ++acceptedCycles_;
    }
    else
    {
        ++rejectedCycles_;
    }

    transitionTo(CycleState::CycleComplete);

    Logger::info(
        "Production cycle completed successfully."
    );

    return true;
}

bool SequenceController::resetForNextCycle()
{
    if (currentState_ != CycleState::CycleComplete)
    {
        Logger::warning(
            "Cycle reset rejected because the current cycle is not complete."
        );

        return false;
    }

    transitionTo(CycleState::WaitingForPart);

    Logger::info(
        "Sequence ready for next part."
    );

    return true;
}

unsigned int SequenceController::getTotalCycles() const
{
    return totalCycles_;
}

unsigned int SequenceController::getAcceptedCycles() const
{
    return acceptedCycles_;
}

unsigned int SequenceController::getRejectedCycles() const
{
    return rejectedCycles_;
}

void SequenceController::transitionTo(
    CycleState state
)
{
    Logger::state(
        "Cycle: "
        + toString(currentState_)
        + " -> "
        + toString(state)
    );

    currentState_ = state;
}

bool SequenceController::verifyDevicesReady() const
{
    return robot_.isInitialized()
        && conveyor_.isInitialized()
        && gripper_.isInitialized()
        && partSensor_.isInitialized();
}

bool SequenceController::failCycle(
    const char* reason
)
{
    Logger::error(reason);

    transitionTo(CycleState::CycleFaulted);

    return false;
}

} // namespace workcell