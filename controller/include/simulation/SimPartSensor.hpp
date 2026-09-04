#pragma once

#include "hardware/ISensor.hpp"

namespace workcell {

class SimPartSensor : public ISensor
{
public:
    SimPartSensor();

    bool initialize() override;

    bool isActive() const override;

    bool isInitialized() const override;

    bool isHealthy() const override;

    void setActive(bool active);

    void setFailure(bool enabled);

private:
    bool initialized_;
    bool active_;
    bool failure_;
};

}
