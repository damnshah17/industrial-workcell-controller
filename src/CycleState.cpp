#include "sequence/CycleState.hpp"

namespace workcell {

std::string toString(CycleState state)
{
    switch (state)
    {
        case CycleState::WaitingForPart:
            return "WaitingForPart";

        case CycleState::StoppingConveyor:
            return "StoppingConveyor";

        case CycleState::MovingToPick:
            return "MovingToPick";

        case CycleState::ClosingGripper:
            return "ClosingGripper";

        case CycleState::MovingToInspection:
            return "MovingToInspection";

        case CycleState::Inspecting:
            return "Inspecting";

        case CycleState::MovingToAcceptBin:
            return "MovingToAcceptBin";

        case CycleState::MovingToRejectBin:
            return "MovingToRejectBin";

        case CycleState::ReleasingPart:
            return "ReleasingPart";

        case CycleState::ReturningHome:
            return "ReturningHome";

        case CycleState::RestartingConveyor:
            return "RestartingConveyor";

        case CycleState::CycleComplete:
            return "CycleComplete";

        case CycleState::CycleFaulted:
            return "CycleFaulted";
    }

    return "Unknown";
}

} // namespace workcell