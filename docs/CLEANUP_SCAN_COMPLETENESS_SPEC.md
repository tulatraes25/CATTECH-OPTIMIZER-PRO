# SPEC Kiro — P1: no successful partial cleanup scan

## GOAL
An error reading *any* candidate file must not be presented as a
complete, trustworthy estimate of files/bytes eligible for cleanup.

## SOURCE EVIDENCE
Before this change, `ScanFolder` and `ScanThumbnailsFolder`
caught `IOException` and `UnauthorizedAccessException` per file with empty
handlers, but then set `IsAccessible=true`, restored
`IsSelected` and displayed an incomplete count as a successful scan.
`CleanupAsync` has a separate deletion preflight but the user-facing
estimate was misleading.

## IMPLEMENTATION
Shared `TrySummarizeCandidates` evaluates each bounded candidate.
It returns false and zero counts on metadata I/O, access or security
errors, malformed paths and checked integer overflow; callers leave
`IsAccessible=false`, `IsSelected=false` and show a sanitized message.
Protected candidates are valid *skips*, not inspection errors.
No direct deletion, new privileged operation, new cleanup target,
background scheduling or OS permissions introduced.

## ACCEPTANCE
- A synthetic successful candidate followed by an unreadable one
  returns an incomplete scan with zero counts.
- A protected file is skipped while the safe count remains accurate.
- An overflowing size estimate fails closed, not a negative/partial number.
- Full Windows restore + Release build + all xUnit tests on exact head
  must pass with zero failures/skips; raw job logs inspected.
- No real user directories, cleanup, SMART or repairs executed in tests.

## LIMITS
This strengthens correctness of UI scan results; it does not solve
cross-process root replacement, reparse-point TOCTOU or make cleanup
safe on real hardware without separately certified Windows QA.
The source code may continue to have existing 384 warning lines;
warnings are tracked separately.

STATUS: DRAFT / CI PENDING. Stack follows PR #62 certified source,
and must never be directly merged into published main.

## Stage 2: only authorized roots may be scanned

The selection UI is not an authorization boundary. Previously,
`ScanTargetsAsync` rejected redirected USER_TEMP and WINDOWS_TEMP, but
still called `ScanThumbnailsFolder` on any path labeled THUMBNAILS
and `ScanFolder` for an unknown target ID. This could enumerate data
outside the intended cleanup scope, even though later deletion was denied.

Scan and delete now share `GetTargetRejectionReason` with a
`requireCompletedScan=false` mode during preflight. This checks the
existing allowlist, exact canonical known-folder root, safe ancestor
chain, Windows requirement and target existence **before** enumerating.
During final cleanup the default true mode still requires an actual
successful scan. The public test fixture `ScanFolder` internal helper
is not an authorization API and remains used solely on temporary data.

Four synthetic cases (THUMBNAILS, USER_TEMP, WINDOWS_TEMP and unknown ID)
verify forged roots remain unavailable, unchecked and return zero counts,
with no file deletion or exposure in scan notes.

Neither the new preflight nor the earlier metadata check is an
identity-preserving filesystem transaction: races still require separate
Windows hardware/file-handle acceptance tests.

## Stage 3: privacy-safe, non-stale WPF scan state

Previously, the WPF Scan command included the exception's raw
`ex.Message`, which may contain private absolute paths, usernames
or host details. It also retained the prior `_targets` and visible
checkboxes during a failed rescan, despite clearing consent.

Now the scan clears prior selections, rows and totals before awaiting
the provider; on failure it displays a constant, actionable error.
A successful scan reports 'sin ubicaciones disponibles' rather than
'listo para limpiar' when all candidates are inaccessible. The
intentionally disabled recycle bin is excluded from partial-scan
warnings so a safe healthy TEMP scan remains plainly actionable.

Synthetic tests cover raw PRIVATE path exception suppression, stale
approval revocation, no-available-target UX, and preserving a normal
ready status when only Recycle Bin is disabled. No real user directories
or cleanup are used.

This is an interface state/privacy fix, not hardware certification,
no permission elevation and no extra deletion capability.
