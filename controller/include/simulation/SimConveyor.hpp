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

    void setStartFailure(
        bool enabled
    );

    void setStopFailure(
        bool enabled
    );

private:
    bool initialized_;
    bool running_;

    bool startFailure_;
    bool stopFailure_;
};

} // namespace workcell