# SPEC Kiro — revoke stale detected hardware on case identity changes

## Risk
ClientEquipmentViewModel correctly discarded an in-flight hardware
response after `NewRecord`, but changing the active client name or
equipment brand/model/type/serial while detection was in progress
did not advance its generation token. A machine's read-only observed
telemetry could therefore appear attached to an edited case.
Completed telemetry could also be carried into the next selected
equipment without pressing NewRecord.

## Goal
No previously captured or pending hardware measurement may be used
for a newly selected customer/equipment identity without a fresh
explicit detection.

## Implementation
On changes to ClientName or EquipmentBrand, EquipmentModel,
SerialNumber or EquipmentType:
- Advance local detection generation, discard late completions;
- Clear all captured OS/CPU/RAM/disk/volume/hostname/username fields;
- Clear hidden disk validity and WindowsVersion;
- Reset IsDetecting/HasDetectedData and show 'Sin detectar';
- Mark current form dirty, preserving prior save-revision guarantees.

Phone/email/notes edits remain ordinary metadata changes and do
not imply an equipment switch.

## Acceptance
Five synthetic completed-hardware scenarios and two pending async
scenarios: changing any identity field clears all measured values;
a subsequent Save cannot persist the other client's OS/disk; late
success and late private exception cannot refill new case.
Full Windows Restore/Release build/xUnit 0 failure/skips; inspect
exact SHA CI logs. No real WMI or user files in tests.

## Limits
This is a UI logical-case safety boundary, NOT physical identity
attestation. The technician must still verify which device is being
tested. No real DiagnosticSessionCoordinator integration yet, no
changes to the passport consent architecture, main, or release.
Status: DRAFT / CI pending.
