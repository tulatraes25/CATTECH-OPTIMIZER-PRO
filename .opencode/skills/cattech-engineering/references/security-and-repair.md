# CATTECH security and repair gates

## Read-only BSOD and DumpChk

For `CrashDumpDiscoveryService`, `DumpCheckPathPolicy`, `DumpCheckProcessRunner` and similar components:

- Enumerate only requested Windows minidumps, without copying/uploading dumps or logging full user paths. Provide no-dump, inaccessible and unsupported states distinctly.
- Treat dump filenames as untrusted. Reject traversal, absolute paths, alternate data streams and directory separators. Also consider directory junctions, file symlinks/reparse points, replacement between discovery and use, and races during execution.
- Require a trustworthy executable origin (SDK known directory); do not search arbitrary current directory, temp folder or PATH. Launch by `ProcessStartInfo.ArgumentList`, never shell interpolation. Bound output/time and kill process tree after cancellation. Do not fetch public symbols or transmit dumps without opt-in.
- Explicitly distinguish `path validation`, `reparse-point checks`, and `identity-preserving open/handle` guarantees. Ordinary `File.Exists` plus an attributes check cannot prove a strong TOCTOU prevention claim. If the execution model cannot guarantee identity, disclose the residual race and block untrusted inputs rather than claim fully safe.
- Test with controlled fixtures for normal dump, invalid basename, symlink and junction scenarios where possible; permissions denied, missing directory, cancellation, timeout, large output, tool missing, parser unknown. Do not execute a real dump processor in unit tests.
- A bugcheck code is evidence of symptom, not proof that a specific driver is culpable. Record module candidates and confidence separately.

## Windows Update

Begin with read-only service/configuration/component inspection. Propose reversible repairs and preflight admin status before mutation. Record original service states and restore policy; stop operations only as needed. Avoid indiscriminate cache deletion, permanent disablement, system security downgrade, or automatic reboot. Use confirmation, timeout/cancel semantics, audit trail and post-repair checks.

## SFC / DISM

`CheckHealth`, `ScanHealth`, and `verifyonly` are read-only in intent but can still write Windows logs; do not label them as literally zero-system-write. `RestoreHealth` and `scannow` can modify Windows: require explicit confirmation, administrator preflight, backup/recovery guidance and error/status interpretation. Fixed allowlisted executables/arguments; no user-supplied shell fragments. Track exit codes and messages without claiming repair success from code 0 alone. If system/disk health is critical, backup first.

## QA and privacy

Synthetic client/technician/equipment identities for reproducible tests. If hardware evidence is needed, redact computer name, username, serial, IP/MAC, absolute paths and sensitive dump contents. Keep local QA evidence local unless user explicitly authorizes publishing it.
