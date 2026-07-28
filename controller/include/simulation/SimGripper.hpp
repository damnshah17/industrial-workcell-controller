#pragma once

#include "hardware/IGripper.hpp"

namespace workcell {

class SimGripper : public IGripper
{
public:
    SimGripper();

    bool initialize() override;

    bool open() override;

    bool close() override;

    bool isOpen() const override;

    bool isInitialized() const override;

    void setOpenFailure(
        bool enabled
    );

    void setCloseFailure(
        bool enabled
    );

private:
    bool initialized_;
    bool open_;

    bool openFailure_;
    bool closeFailure_;
};

} // namespace workcell