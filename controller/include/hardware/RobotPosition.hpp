#pragma once

#include <string>

namespace workcell {

enum class RobotPosition
{
    Home,
    Pick,
    Inspect,
    AcceptBin,
    RejectBin
};

std::string toString(RobotPosition position);

}