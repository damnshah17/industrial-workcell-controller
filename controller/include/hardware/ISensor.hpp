#pragma once

namespace workcell {

class ISensor
{
public:
    virtual ~ISensor() = default;

    virtual bool initialize() = 0;

    virtual bool isActive() const = 0;

    virtual bool isInitialized() const = 0;

    virtual bool isHealthy() const = 0;
};

}
