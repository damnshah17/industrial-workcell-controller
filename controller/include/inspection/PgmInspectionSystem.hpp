#pragma once

#include "inspection/IInspectionSystem.hpp"

#include <filesystem>
#include <string>
#include <unordered_map>

namespace workcell {

class PgmInspectionSystem final : public IInspectionSystem
{
public:
    explicit PgmInspectionSystem(std::filesystem::path sampleRoot);

    InspectionResult inspect(const std::string& sampleId) override;
    bool isKnownSample(const std::string& sampleId) const;

private:
    std::filesystem::path sampleRoot_;
    const std::unordered_map<std::string, std::filesystem::path> samples_;
};

} // namespace workcell
