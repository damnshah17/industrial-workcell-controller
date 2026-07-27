cmake_minimum_required(VERSION 3.20)

project(
    IndustrialWorkcellController
    VERSION 0.2.0
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# ==========================================
# Controller library
# ==========================================

add_library(
    workcell_controller

    src/FaultManager.cpp
    src/SafetyController.cpp
    src/MachineState.cpp
    src/MachineController.cpp
    src/Command.cpp
    src/Fault.cpp
    src/Logger.cpp

    src/RobotPosition.cpp
    src/SimRobotArm.cpp
    src/SimConveyor.cpp
    src/SimGripper.cpp
    src/SimPartSensor.cpp

    src/CycleState.cpp
    src/SequenceController.cpp
)

target_include_directories(
    workcell_controller
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# ==========================================
# Main CLI
# ==========================================

add_executable(
    workcell_cli
    apps/controller_cli.cpp
)

target_link_libraries(
    workcell_cli
    PRIVATE
        workcell_controller
)

# ==========================================
# Cycle demo
# ==========================================

add_executable(
    cycle_demo
    apps/cycle_demo.cpp
)

target_link_libraries(
    cycle_demo
    PRIVATE
        workcell_controller
)

# ==========================================
# Testing
# ==========================================

include(CTest)

if(BUILD_TESTING)

    include(FetchContent)

    FetchContent_Declare(
        googletest
        URL https://github.com/google/googletest/archive/refs/tags/v1.17.0.zip
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )

    set(
        gtest_force_shared_crt
        ON
        CACHE BOOL ""
        FORCE
    )

    FetchContent_MakeAvailable(googletest)

    add_executable(
        workcell_tests

        tests/MachineControllerTests.cpp
        tests/CommandTests.cpp
        tests/SimHardwareTests.cpp
        tests/SequenceControllerTests.cpp
    )

    target_link_libraries(
        workcell_tests
        PRIVATE
            workcell_controller
            GTest::gtest_main
    )

    include(GoogleTest)

    gtest_discover_tests(workcell_tests)

endif()