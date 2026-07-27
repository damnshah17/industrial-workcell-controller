#pragma once

#include "hardware/IConveyor.hpp"

namespace workcell {

class SimConveyor : public IConveyor
{
public:
    SimConveyor();

    bool initialize() override;

    bool start() override;

    bool stop() override;

    bool isRunning() const override;

    bool isInitialized() const override;

private:
    bool initialized_;
    bool running_;
};

}