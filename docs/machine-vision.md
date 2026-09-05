# Machine vision inspection

Phase 8 replaces the normal caller-supplied pass/fail decision with a C++ inspection result. The operator selects a known simulation sample; ASP.NET and the bridge pass only that identifier to the controller. `SequenceController` invokes `IInspectionSystem` at the `Inspecting` state and uses its result to select `AcceptBin` or `RejectBin`.

## Inspection method

`PgmInspectionSystem` reads small ASCII PGM images and applies deterministic classical image processing:

1. Threshold pixels into foreground and background.
2. Measure the dark part body's bounding box against geometry tolerances.
3. Measure bright-pixel coverage in the expected central circular-feature region.

The coverage is an observed image measurement, not an invented confidence score. A valid body with sufficient opening coverage passes. A missing opening or malformed body is rejected normally. An unknown or undecodable image produces `INSPECTION_FAILURE` through the normal sequence fault path.

## Known samples

- `good-part`: valid rectangular part with the required central opening.
- `missing-hole`: valid body without the required opening.
- `malformed-part`: body dimensions outside tolerance.
- `unreadable-part`: deterministic decode-error test input.

The API does not accept arbitrary filesystem paths.

## API

Start a vision-controlled production cycle:

```http
POST /api/machine/cycle
Content-Type: application/json

{ "sampleId": "good-part" }
```

The normal telemetry response includes a compact `inspection` object with state, accepted result, reason, sample ID, feature coverage, and diagnostic details. Before an inspection completes its state is `Idle`.

The legacy `inspectionAccepted` request and bridge commands remain available as a manual compatibility override for existing demos and tests. New operator flows use `sampleId`.

## Persistence

Completed production-cycle history stores the controller-reported result, reason, sample identifier, and measured feature coverage. Images are not stored in PostgreSQL.
