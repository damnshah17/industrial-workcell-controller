#pragma once

namespace workcell {

class IConveyor
{
public:
    virtual ~IConveyor() = default;

    virtual bool initialize() = 0;

    virtual bool start() = 0;

    virtual bool stop() = 0;

    virtual bool isRunning() const = 0;

    virtual bool isInitialized() const = 0;
};

}