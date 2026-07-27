#include "sequence/SequenceController.hpp"

#include "hardware/RobotPosition.hpp"
#include "logging/Logger.hpp"

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
      rejectedCycles_(0),
      inspectionAccepted_(std::nullopt),
      fault_(std::nullopt),
      motionTimeout_(
          std::chrono::milliseconds(3000)
      ),
      stateStartTime_(
          std::chrono::steady_clock::now()
      )
{
}

CycleState SequenceController::getState() const
{
    return currentState_;
}

bool SequenceController::startCycle(
    bool inspectionAccepted
)
{
    if (!verifyDevicesReady())
    {
        failCycle(
            FaultCode::InitializationFailure,
            "Cannot start cycle because hardware is not ready."
        );

        return false;
    }

    if (
        currentState_
        != CycleState::WaitingForPart
    )
    {
        Logger::warning(
            "Cycle start rejected because sequence is not waiting for a part."
        );

        return false;
    }

    if (!partSensor_.isActive())
    {
        Logger::info(
            "Cycle start ignored because no part is detected."
        );

        return false;
    }

    fault_.reset();

    inspectionAccepted_ =
        inspectionAccepted;

    Logger::info(
        "Part detected. Production cycle started."
    );

    transitionTo(
        CycleState::StoppingConveyor
    );

    return true;
}

void SequenceController::update()
{
    robot_.update();

    switch (currentState_)
    {
        case CycleState::WaitingForPart:
            return;

        case CycleState::StoppingConveyor:
        {
            if (!conveyor_.stop())
            {
                failCycle(
                    FaultCode::ConveyorFailure,
                    "Failed to stop conveyor."
                );

                return;
            }

            transitionTo(
                CycleState::MovingToPick
            );

            if (!robot_.moveTo(
                    RobotPosition::Pick
                ))
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Failed to start robot motion to Pick."
                );
            }

            return;
        }

        case CycleState::MovingToPick:
        {
            if (hasStateTimedOut())
            {
                failCycle(
                    FaultCode::MotionTimeout,
                    "Robot motion to Pick timed out."
                );

                return;
            }

            if (robot_.isMoving())
            {
                return;
            }

            if (
                robot_.getPosition()
                != RobotPosition::Pick
            )
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Robot stopped before reaching Pick."
                );

                return;
            }

            transitionTo(
                CycleState::ClosingGripper
            );

            return;
        }

        case CycleState::ClosingGripper:
        {
            if (!gripper_.close())
            {
                failCycle(
                    FaultCode::GripperFailure,
                    "Gripper failed to close."
                );

                return;
            }

            transitionTo(
                CycleState::MovingToInspection
            );

            if (!robot_.moveTo(
                    RobotPosition::Inspect
                ))
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Failed to start robot motion to Inspect."
                );
            }

            return;
        }

        case CycleState::MovingToInspection:
        {
            if (hasStateTimedOut())
            {
                failCycle(
                    FaultCode::MotionTimeout,
                    "Robot motion to Inspect timed out."
                );

                return;
            }

            if (robot_.isMoving())
            {
                return;
            }

            if (
                robot_.getPosition()
                != RobotPosition::Inspect
            )
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Robot stopped before reaching Inspect."
                );

                return;
            }

            transitionTo(
                CycleState::Inspecting
            );

            return;
        }

        case CycleState::Inspecting:
        {
            if (!inspectionAccepted_.has_value())
            {
                failCycle(
                    FaultCode::InspectionFailure,
                    "Inspection result is unavailable."
                );

                return;
            }

            if (inspectionAccepted_.value())
            {
                Logger::info(
                    "Inspection result: ACCEPTED."
                );

                transitionTo(
                    CycleState::MovingToAcceptBin
                );

                if (!robot_.moveTo(
                        RobotPosition::AcceptBin
                    ))
                {
                    failCycle(
                        FaultCode::RobotCommunicationLoss,
                        "Failed to start motion to AcceptBin."
                    );
                }
            }
            else
            {
                Logger::info(
                    "Inspection result: REJECTED."
                );

                transitionTo(
                    CycleState::MovingToRejectBin
                );

                if (!robot_.moveTo(
                        RobotPosition::RejectBin
                    ))
                {
                    failCycle(
                        FaultCode::RobotCommunicationLoss,
                        "Failed to start motion to RejectBin."
                    );
                }
            }

            return;
        }

        case CycleState::MovingToAcceptBin:
        {
            if (hasStateTimedOut())
            {
                failCycle(
                    FaultCode::MotionTimeout,
                    "Robot motion to AcceptBin timed out."
                );

                return;
            }

            if (robot_.isMoving())
            {
                return;
            }

            if (
                robot_.getPosition()
                != RobotPosition::AcceptBin
            )
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Robot stopped before reaching AcceptBin."
                );

                return;
            }

            transitionTo(
                CycleState::ReleasingPart
            );

            return;
        }

        case CycleState::MovingToRejectBin:
        {
            if (hasStateTimedOut())
            {
                failCycle(
                    FaultCode::MotionTimeout,
                    "Robot motion to RejectBin timed out."
                );

                return;
            }

            if (robot_.isMoving())
            {
                return;
            }

            if (
                robot_.getPosition()
                != RobotPosition::RejectBin
            )
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Robot stopped before reaching RejectBin."
                );

                return;
            }

            transitionTo(
                CycleState::ReleasingPart
            );

            return;
        }

        case CycleState::ReleasingPart:
        {
            if (!gripper_.open())
            {
                failCycle(
                    FaultCode::GripperFailure,
                    "Gripper failed to release part."
                );

                return;
            }

            transitionTo(
                CycleState::ReturningHome
            );

            if (!robot_.moveTo(
                    RobotPosition::Home
                ))
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Failed to start return motion."
                );
            }

            return;
        }

        case CycleState::ReturningHome:
        {
            if (hasStateTimedOut())
            {
                failCycle(
                    FaultCode::MotionTimeout,
                    "Robot return-to-home timed out."
                );

                return;
            }

            if (robot_.isMoving())
            {
                return;
            }

            if (
                robot_.getPosition()
                != RobotPosition::Home
            )
            {
                failCycle(
                    FaultCode::RobotCommunicationLoss,
                    "Robot stopped before reaching Home."
                );

                return;
            }

            transitionTo(
                CycleState::RestartingConveyor
            );

            return;
        }

        case CycleState::RestartingConveyor:
        {
            if (!conveyor_.start())
            {
                failCycle(
                    FaultCode::ConveyorFailure,
                    "Failed to restart conveyor."
                );

                return;
            }

            ++totalCycles_;

            if (
                inspectionAccepted_.value_or(false)
            )
            {
                ++acceptedCycles_;
            }
            else
            {
                ++rejectedCycles_;
            }

            transitionTo(
                CycleState::CycleComplete
            );

            Logger::info(
                "Production cycle completed."
            );

            return;
        }

        case CycleState::CycleComplete:
            return;

        case CycleState::CycleFaulted:
            return;
    }
}

void SequenceController::abort()
{
    robot_.stop();
    conveyor_.stop();

    Logger::warning(
        "Production sequence aborted."
    );
}

bool SequenceController::resetForNextCycle()
{
    if (
        currentState_ != CycleState::CycleComplete
        &&
        currentState_ != CycleState::CycleFaulted
    )
    {
        return false;
    }

    inspectionAccepted_.reset();
    fault_.reset();

    transitionTo(
        CycleState::WaitingForPart
    );

    return true;
}

const std::optional<Fault>&
SequenceController::getFault() const
{
    return fault_;
}

unsigned int
SequenceController::getTotalCycles() const
{
    return totalCycles_;
}

unsigned int
SequenceController::getAcceptedCycles() const
{
    return acceptedCycles_;
}

unsigned int
SequenceController::getRejectedCycles() const
{
    return rejectedCycles_;
}

void SequenceController::setMotionTimeout(
    std::chrono::milliseconds timeout
)
{
    motionTimeout_ = timeout;
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

    stateStartTime_ =
        std::chrono::steady_clock::now();
}

bool SequenceController::verifyDevicesReady() const
{
    return robot_.isInitialized()
        && conveyor_.isInitialized()
        && gripper_.isInitialized()
        && partSensor_.isInitialized();
}

bool SequenceController::hasStateTimedOut() const
{
    const auto elapsed =
        std::chrono::duration_cast<
            std::chrono::milliseconds
        >(
            std::chrono::steady_clock::now()
            - stateStartTime_
        );

    return elapsed > motionTimeout_;
}

void SequenceController::failCycle(
    FaultCode code,
    const char* reason
)
{
    Logger::error(reason);

    fault_ = Fault{
        code,
        reason
    };

    robot_.stop();
    conveyor_.stop();

    transitionTo(
        CycleState::CycleFaulted
    );
}

} // namespace workcell