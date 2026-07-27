#include <gtest/gtest.h>

#include "machine/Command.hpp"

using workcell::CommandType;
using workcell::parseCommand;

TEST(CommandTest, ParsesStartCommand)
{
    const auto command =
        parseCommand("start");

    EXPECT_EQ(
        command.type,
        CommandType::Start
    );
}

TEST(CommandTest, CommandParsingIsCaseInsensitive)
{
    const auto command =
        parseCommand("START");

    EXPECT_EQ(
        command.type,
        CommandType::Start
    );
}

TEST(CommandTest, ParsesEmergencyStopCommand)
{
    const auto command =
        parseCommand("estop");

    EXPECT_EQ(
        command.type,
        CommandType::EmergencyStop
    );
}

TEST(CommandTest, FaultCommandPreservesArgument)
{
    const auto command =
        parseCommand(
            "fault robot failed to reach position"
        );

    EXPECT_EQ(
        command.type,
        CommandType::InjectFault
    );

    EXPECT_EQ(
        command.argument,
        "robot failed to reach position"
    );
}

TEST(CommandTest, UnknownCommandReturnsInvalid)
{
    const auto command =
        parseCommand("banana");

    EXPECT_EQ(
        command.type,
        CommandType::Invalid
    );
}