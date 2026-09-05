#include "inspection/PgmInspectionSystem.hpp"

#include <algorithm>
#include <fstream>
#include <limits>
#include <vector>

namespace workcell {
namespace {

struct Image
{
    int width{};
    int height{};
    int maximum{};
    std::vector<int> pixels;
};

bool readPgm(const std::filesystem::path& path, Image& image)
{
    std::ifstream input(path);
    std::string magic;
    if (!(input >> magic >> image.width >> image.height >> image.maximum)
        || magic != "P2"
        || image.width <= 0
        || image.height <= 0
        || image.maximum <= 0)
    {
        return false;
    }

    image.pixels.reserve(
        static_cast<std::size_t>(image.width * image.height)
    );
    int pixel = 0;
    while (input >> pixel)
    {
        if (pixel < 0 || pixel > image.maximum)
        {
            return false;
        }
        image.pixels.push_back(pixel);
    }

    return image.pixels.size()
        == static_cast<std::size_t>(image.width * image.height);
}

} // namespace

PgmInspectionSystem::PgmInspectionSystem(
    std::filesystem::path sampleRoot
)
    : sampleRoot_(std::move(sampleRoot)),
      samples_({
          {"good-part", "accepted/good-part.pgm"},
          {"missing-hole", "rejected/missing-hole.pgm"},
          {"malformed-part", "rejected/malformed-part.pgm"},
          {"unreadable-part", "rejected/unreadable-part.pgm"}
      })
{
}

bool PgmInspectionSystem::isKnownSample(const std::string& sampleId) const
{
    return samples_.contains(sampleId);
}

InspectionResult PgmInspectionSystem::inspect(const std::string& sampleId)
{
    const auto sample = samples_.find(sampleId);
    if (sample == samples_.end())
    {
        return {false, InspectionReason::InspectionError, sampleId, 0.0,
            "Unknown inspection sample."};
    }

    Image image;
    if (!readPgm(sampleRoot_ / sample->second, image))
    {
        return {false, InspectionReason::InspectionError, sampleId, 0.0,
            "Inspection image could not be decoded."};
    }

    const int darkThreshold = image.maximum / 2;
    int minX = image.width;
    int minY = image.height;
    int maxX = -1;
    int maxY = -1;
    int darkPixels = 0;

    for (int y = 0; y < image.height; ++y)
    {
        for (int x = 0; x < image.width; ++x)
        {
            if (image.pixels[y * image.width + x] < darkThreshold)
            {
                ++darkPixels;
                minX = std::min(minX, x);
                minY = std::min(minY, y);
                maxX = std::max(maxX, x);
                maxY = std::max(maxY, y);
            }
        }
    }

    if (darkPixels == 0)
    {
        return {false, InspectionReason::GeometryMismatch, sampleId, 0.0,
            "No part body was detected."};
    }

    const int bodyWidth = maxX - minX + 1;
    const int bodyHeight = maxY - minY + 1;
    if (bodyWidth < image.width * 55 / 100
        || bodyHeight < image.height * 55 / 100
        || bodyWidth > image.width * 80 / 100
        || bodyHeight > image.height * 80 / 100)
    {
        return {false, InspectionReason::GeometryMismatch, sampleId, 0.0,
            "Detected part geometry is outside tolerance."};
    }

    const int centerX = (minX + maxX) / 2;
    const int centerY = (minY + maxY) / 2;
    const int radius = std::max(2, std::min(bodyWidth, bodyHeight) / 5);
    int roiPixels = 0;
    int brightPixels = 0;

    for (int y = centerY - radius; y <= centerY + radius; ++y)
    {
        for (int x = centerX - radius; x <= centerX + radius; ++x)
        {
            if (x < 0 || y < 0 || x >= image.width || y >= image.height)
            {
                continue;
            }
            const int dx = x - centerX;
            const int dy = y - centerY;
            if (dx * dx + dy * dy <= radius * radius)
            {
                ++roiPixels;
                if (image.pixels[y * image.width + x] >= darkThreshold)
                {
                    ++brightPixels;
                }
            }
        }
    }

    const double coverage = roiPixels == 0
        ? 0.0
        : static_cast<double>(brightPixels) / roiPixels;
    if (coverage < 0.65)
    {
        return {false, InspectionReason::MissingFeature, sampleId, coverage,
            "Required central opening was not detected."};
    }

    return {true, InspectionReason::Pass, sampleId, coverage,
        "Part geometry and required opening are within tolerance."};
}

} // namespace workcell
