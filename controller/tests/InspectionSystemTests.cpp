#include "inspection/PgmInspectionSystem.hpp"

#include <gtest/gtest.h>

namespace {

TEST(PgmInspectionSystemTests, ValidPartPassesFeatureInspection)
{
    workcell::PgmInspectionSystem inspection(WORKCELL_INSPECTION_SAMPLE_DIR);
    const auto result = inspection.inspect("good-part");

    EXPECT_TRUE(result.accepted);
    EXPECT_EQ(result.reason, workcell::InspectionReason::Pass);
    EXPECT_GT(result.featureCoverage, 0.65);
}

TEST(PgmInspectionSystemTests, MissingOpeningHasDeterministicReason)
{
    workcell::PgmInspectionSystem inspection(WORKCELL_INSPECTION_SAMPLE_DIR);
    const auto result = inspection.inspect("missing-hole");

    EXPECT_FALSE(result.accepted);
    EXPECT_EQ(result.reason, workcell::InspectionReason::MissingFeature);
}

TEST(PgmInspectionSystemTests, MalformedBodyFailsGeometryTolerance)
{
    workcell::PgmInspectionSystem inspection(WORKCELL_INSPECTION_SAMPLE_DIR);
    const auto result = inspection.inspect("malformed-part");

    EXPECT_FALSE(result.accepted);
    EXPECT_EQ(result.reason, workcell::InspectionReason::GeometryMismatch);
}

TEST(PgmInspectionSystemTests, DecodeFailureIsAnInspectionError)
{
    workcell::PgmInspectionSystem inspection(WORKCELL_INSPECTION_SAMPLE_DIR);
    const auto result = inspection.inspect("unreadable-part");

    EXPECT_FALSE(result.accepted);
    EXPECT_EQ(result.reason, workcell::InspectionReason::InspectionError);
}

} // namespace
