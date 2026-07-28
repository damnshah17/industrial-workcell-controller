#pragma once

namespace workcell {

class IGripper
{
public:
    virtual ~IGripper() = default;

    virtual bool initialize() = 0;

    virtual bool open() = 0;

    virtual bool close() = 0;

    virtual bool isOpen() const = 0;

    virtual bool isInitialized() const = 0;
};

}