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
