#include "faults/FaultManager.hpp"

#include "logging/Logger.hpp"

namespace workcell {

bool FaultManager::raiseFault(
    FaultCode code,
    const std::string& message
)
{
    if (activeFault_.has_value())
    {
        Logger::warning(
            "Fault raise rejected because another fault is already active."
        );

        return false;
    }

    activeFault_ = Fault{
        code,
        message
    };

    Logger::error(
        "Fault raised: "
        + toString(code)
        + " - "
        + message
    );

    return true;
}

void FaultManager::clearFault()
{
    if (!activeFault_.has_value())
    {
        return;
    }

    Logger::info(
        "Fault cleared: "
        + toString(activeFault_->code)
    );

    activeFault_.reset();
}

bool FaultManager::hasActiveFault() const
{
    return activeFault_.has_value();
}

const std::optional<Fault>&
FaultManager::getActiveFault() const
{
    return activeFault_;
}

} // namespace workcell