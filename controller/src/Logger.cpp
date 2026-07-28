#include "logging/Logger.hpp"

#include <chrono>
#include <ctime>
#include <iomanip>
#include <iostream>

namespace workcell {

void Logger::log(
    LogLevel level,
    const std::string& message
)
{
    const auto now =
        std::chrono::system_clock::now();

    const std::time_t currentTime =
        std::chrono::system_clock::to_time_t(now);

    std::tm localTime {};

#ifdef _WIN32
    localtime_s(&localTime, &currentTime);
#else
    localtime_r(&currentTime, &localTime);
#endif

    std::cout
        << "["
        << std::put_time(&localTime, "%H:%M:%S")
        << "] ["
        << levelToString(level)
        << "] "
        << message
        << '\n';
}

void Logger::info(const std::string& message)
{
    log(LogLevel::Info, message);
}

void Logger::state(const std::string& message)
{
    log(LogLevel::State, message);
}

void Logger::warning(const std::string& message)
{
    log(LogLevel::Warning, message);
}

void Logger::error(const std::string& message)
{
    log(LogLevel::Error, message);
}

void Logger::safety(const std::string& message)
{
    log(LogLevel::Safety, message);
}

std::string Logger::levelToString(LogLevel level)
{
    switch (level)
    {
        case LogLevel::Info:
            return "INFO";

        case LogLevel::State:
            return "STATE";

        case LogLevel::Warning:
            return "WARN";

        case LogLevel::Error:
            return "ERROR";

        case LogLevel::Safety:
            return "SAFETY";
    }

    return "UNKNOWN";
}

}