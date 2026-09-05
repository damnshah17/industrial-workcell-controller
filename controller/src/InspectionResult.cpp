#include "inspection/InspectionResult.hpp"

namespace workcell {

std::string toString(InspectionReason reason)
{
    switch (reason)
    {
        case InspectionReason::Pass:
            return "PASS";
        case InspectionReason::MissingFeature:
            return "MISSING_FEATURE";
        case InspectionReason::GeometryMismatch:
            return "GEOMETRY_MISMATCH";
        case InspectionReason::InspectionError:
            return "INSPECTION_ERROR";
    }

    return "INSPECTION_ERROR";
}

} // namespace workcell
