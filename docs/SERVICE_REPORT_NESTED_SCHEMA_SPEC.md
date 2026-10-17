# CATTECH — SPEC Kiro #58: persisted report structural validation

## GOAL
Never interpret a malformed persisted service report as a healthy client/equipment
record merely because .NET object initializers generated missing nested sections.

## Canonical read contract
For legacy and new JSON service reports, require exactly one camelCase
`id` (nonblank string), `client` (object), `equipment` (object) and `service`
(object). Duplicate keys, including case-variant duplicates, fail closed.
When `service.actionsPerformed` or `service.recommendations` is present,
it must be an array of JSON strings; when absent, the initialized empty
collection provides backward-compatible defaults. Existing eight-hex-character
legacy IDs remain accepted only if logically unique within the store.
Unknown extra fields are ignored for forward compatibility.

The SAME parser is used by:
- `ListReportsAsync` (legacy listing of individually readable entries);
- `InspectReportInventoryAsync` (aggregate advisory health);
- `LoadReportAsync` and `DeleteReportAsync` (revalidate all known files;
  block even an apparently valid record if any file is malformed or duplicated).

The listing intentionally retains its pre-existing best-effort API. Do not
interpret a returned list row as permission to export or delete. The WPF
report screen checks inventory before customer selection and revalidates before
HTML/PDF generation. It also blocks when a matching record changes on reload.

## Non-goals and residual risks
- No user data migration, automatic deletion, quarantine or repairs.
- No inter-process transaction, handle-pinned filesystem identity, proven
  physical hardware binding, or preservation of a valid snapshot across changes.
- No completion claim for unrelated stored diagnosis/SMART identity checks.
- No real customer files used in tests; only temp-folder synthetic fixtures.

## Acceptance
Null, missing, nonobject, duplicate or case-variant required properties,
and null/malformed service arrays: invalid inventory, skipped legacy list
entry, no load or deletion. Healthy legacy record with short ID and absent
optional arrays remains readable. Corrupt unrelated file blocks load/delete
of other reports without removing either file. Windows CI on exact SHA must
restore/build Release/test full xUnit suite with no failures/skips; interactive
WPF/hardware gate separate.

Status: CODE IMPLEMENTED, tests committed; CI and integration evidence tracked
in PR #59. Issue #58 stays OPEN until release/integration gates are satisfied.
