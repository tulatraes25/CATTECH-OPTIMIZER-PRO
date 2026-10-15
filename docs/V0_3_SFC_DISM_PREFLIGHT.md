# CATTECH v0.3 / R.2.2 - Read-only SFC/DISM preflight

The fixed catalog in SystemRepairPlanService remains the sole source of allowed SFC/DISM operations. R.2.2 evaluates Windows version, elevated administrator status, System32 origin, executable existence and non-reparse attributes. It does not execute commands, create a backup, perform a repair, or authorize execution.

A successful result is intentionally named ReviewRequired, not ReadyToRun. All results expose ExecutionAuthorized=false and require technician review. A later, separately reviewed execution gate must add explicit per-operation consent, safe tool identity at process open time (rather than trusting a prior attributes check), backup policy for modifications, cancellation, bounded output and post-condition verification.

Tests inject a fake platform/privilege/filesystem probe to exercise supported operations, unsupported OS, non-admin, missing binary, reparse directory/binary, and inaccessible inspection. The tests prove only preflight behavior, not real repair effectiveness.

R.2.2 should be accepted only after full Windows CI passes on the exact commit. No change to stable main, published releases, Windows services or user data is permitted in this slice.
