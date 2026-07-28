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

    void setActive(bool active);

private:
    bool initialized_;
    bool active_;
};

}