#pragma once

#include <string>

namespace workcell {

enum class LogLevel
{
    Info,
    State,
    Warning,
    Error,
    Safety
};

class Logger
{
public:
    static void log(
        LogLevel level,
        const std::string& message
    );

    static void info(const std::string& message);

    static void state(const std::string& message);

    static void warning(const std::string& message);

    static void error(const std::string& message);

    static void safety(const std::string& message);

private:
    static std::string levelToString(LogLevel level);
};

}