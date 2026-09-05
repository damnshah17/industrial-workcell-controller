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

    src/InspectionResult.cpp
    src/PgmInspectionSystem.cpp

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

add_executable(
    integrated_demo
    apps/integrated_demo.cpp
)

target_link_libraries(
    integrated_demo
    PRIVATE
        workcell_controller
)

add_executable(
    machine_bridge
    apps/machine_bridge.cpp
)

target_link_libraries(
    machine_bridge
    PRIVATE
        workcell_controller
)

target_compile_definitions(
    machine_bridge
    PRIVATE
        WORKCELL_INSPECTION_SAMPLE_DIR="${CMAKE_CURRENT_SOURCE_DIR}/inspection-samples"
)

if(UNIX)
    target_link_libraries(
        machine_bridge
        PRIVATE
            pthread
    )
endif()
# ==========================================
# Testing
# ==========================================

include(CTest)

if(BUILD_TESTING)

    find_package(Python3 COMPONENTS Interpreter REQUIRED)

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
        tests/InspectionSystemTests.cpp
    )

    target_link_libraries(
        workcell_tests
        PRIVATE
            workcell_controller
            GTest::gtest_main
    )

    include(GoogleTest)

    gtest_discover_tests(workcell_tests)

    add_test(
        NAME MachineBridgeIntegration
        COMMAND
            ${Python3_EXECUTABLE}
            ${CMAKE_CURRENT_SOURCE_DIR}/tests/bridge_integration.py
            $<TARGET_FILE:machine_bridge>
    )

    set_tests_properties(
        MachineBridgeIntegration
        PROPERTIES TIMEOUT 15
    )

    add_test(
        NAME MachineBridgeFaultIntegration
        COMMAND
            ${Python3_EXECUTABLE}
            ${CMAKE_CURRENT_SOURCE_DIR}/tests/bridge_fault_integration.py
            $<TARGET_FILE:machine_bridge>
    )

    set_tests_properties(
        MachineBridgeFaultIntegration
        PROPERTIES TIMEOUT 30
    )

    target_compile_definitions(
        workcell_tests
        PRIVATE
            WORKCELL_INSPECTION_SAMPLE_DIR="${CMAKE_CURRENT_SOURCE_DIR}/inspection-samples"
    )

    add_test(
        NAME MachineBridgeVisionIntegration
        COMMAND
            ${Python3_EXECUTABLE}
            ${CMAKE_CURRENT_SOURCE_DIR}/tests/bridge_vision_integration.py
            $<TARGET_FILE:machine_bridge>
    )

    set_tests_properties(
        MachineBridgeVisionIntegration
        PROPERTIES TIMEOUT 30
    )

endif()
