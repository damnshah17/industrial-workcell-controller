#pragma once

#include <string>

namespace workcell {

enum class CycleState
{
    WaitingForPart,
    StoppingConveyor,
    MovingToPick,
    ClosingGripper,
    MovingToInspection,
    Inspecting,
    MovingToAcceptBin,
    MovingToRejectBin,
    ReleasingPart,
    ReturningHome,
    RestartingConveyor,
    CycleComplete,
    CycleFaulted,
    CycleAborted
};

std::string toString(
    CycleState state
);

} // namespace workcell