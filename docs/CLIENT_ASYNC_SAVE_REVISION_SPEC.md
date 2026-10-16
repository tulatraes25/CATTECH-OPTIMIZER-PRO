# SPEC Kiro — Client/Equipment asynchronous save revision isolation

## Goal
A save started for one client/equipment form must NEVER clear the
unsaved state of a newly selected or edited form when I/O completes.

## Before
`SaveAsync` awaited a storage operation without binding its later
UI state to the original form. If the technician selected New Record
or edited the current form during the await, a completion could set
`HasUnsavedChanges=false` and claim the new form was saved.

## Change
`_formRevision` increments on every client/equipment input mutation
and on NewRecord even when current fields are blank.
`SaveAsync` captures the revision before serializing/saving. A
late completion for a prior revision cannot set the new form clean
or claim current-save success. It displays a safe notice that the
*previous* record completed and the current form remains unsaved.
A late failure is likewise attributed to the previous operation
without exposing exception text or absolute storage paths.

No changes to persistence formats, file deletion, navigation,
physical device attribution, permissions or external services.

## Acceptance
Three synthetic asynchronous tests with explicit pending TaskCompletionSource:
old Client A finishing after Client B, edits during save, old I/O
failure after client switch. Current form retains current values
and unsaved flag; original report carries only A's prior snapshot.
Full Windows Restore/Release build/xUnit at exact SHA with no
failures/skips, logs reviewed.

## Residual
The revision is an in-process UI guard, not a persistence transaction
or a guarantee that an OS failure created no files. Navigation and
hardware logical-case leases remain future separate work.
Status DRAFT / CI PENDING. Do not merge/release.
