#pragma once

#include "inspection/InspectionResult.hpp"

#include <string>

namespace workcell {

class IInspectionSystem
{
public:
    virtual ~IInspectionSystem() = default;
    virtual InspectionResult inspect(const std::string& sampleId) = 0;
};

} // namespace workcell
