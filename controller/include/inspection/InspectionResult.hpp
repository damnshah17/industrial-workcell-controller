#pragma once

#include <string>

namespace workcell {

enum class InspectionReason
{
    Pass,
    MissingFeature,
    GeometryMismatch,
    InspectionError
};

std::string toString(InspectionReason reason);

struct InspectionResult
{
    bool accepted;
    InspectionReason reason;
    std::string sampleId;
    double featureCoverage;
    std::string details;
};

} // namespace workcell
