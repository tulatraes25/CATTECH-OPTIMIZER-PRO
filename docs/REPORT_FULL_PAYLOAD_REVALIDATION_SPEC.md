# CATTECH — SPEC Kiro #60: full customer report revalidation

## Goal
Avoid generating HTML/PDF from a stale in-memory ServiceReport after the
persisted report changed but retained the same client name or equipment ID.

## Behavior
Before customer-bearing output, the ReportViewModel already checks the
read-only inventory and reloads the selected stored report. This change
compares deterministic JSON serialization of the *complete* loaded and
selected ServiceReport models, not just Id/name/model/serial.
Phone, service reason, actions, notes, creation date, technician, and other
serializable fields participate. No payload, hash, filename or exception text
is logged or shown.

Any mismatch revokes the selected client, clears generated-artifact state and
prevents HTML/PDF. A report-free export remains governed by IncludeClient=false;
do not introduce a spurious customer requirement.

## Tests and acceptance
Synthetic fakes return stable timestamps to model persisted records.
Change phone, service reason, service actions or technician while retaining
the original ID/name/model/serial: HTML/PDF must not execute. Existing
unchanged-client and IncludeClient=false tests must still pass.
Full GitHub Windows restore, Release build and xUnit 0 fail/skip on exact SHA.

## Explicit limitations
Full semantic comparison is conservative and may reject in-memory unsaved edits.
It is not a file-handle-pinned transaction: a different process can still edit
the file after revalidation and before output. Serializing the model does not
attest physical hardware or an unverified external JSON field. No file writes,
repairs, releases or destructive operations are authorized by this change.

Status: DRAFT / implementation pending exact SHA CI, integration #43 and real
Windows interactive gate. Linked issue #60.
