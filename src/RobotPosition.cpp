#include "hardware/RobotPosition.hpp"

namespace workcell {

std::string toString(RobotPosition position)
{
    switch (position)
    {
        case RobotPosition::Home:
            return "Home";

        case RobotPosition::Pick:
            return "Pick";

        case RobotPosition::Inspect:
            return "Inspect";

        case RobotPosition::AcceptBin:
            return "AcceptBin";

        case RobotPosition::RejectBin:
            return "RejectBin";
    }

    return "Unknown";
}

}