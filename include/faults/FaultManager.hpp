#pragma once

#include "faults/Fault.hpp"

#include <optional>
#include <string>

namespace workcell {

class FaultManager
{
public:
    bool raiseFault(
        FaultCode code,
        const std::string& message
    );

    void clearFault();

    bool hasActiveFault() const;

    const std::optional<Fault>& getActiveFault() const;

private:
    std::optional<Fault> activeFault_;
};

} // namespace workcell