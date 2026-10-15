# SPEC Kiro — v0.3 Repair Center UI / R.1.2 + R.2.3

## GOAL

Expose existing Windows Update service observation and SFC/DISM fixed repair plans in an understandable, read-only technician UI. Establish the navigation and ViewModel contract before implementing any execution component.

## REQUIREMENTS

1. Add a "Reparaciones" navigation item and WPF view.
2. No platform inspection on navigation or ViewModel construction. Inspect only on user interaction; run slow WMI/preflight checks off the WPF UI thread.
3. Show four Windows Update services with precise states and start modes. "Stopped" is normal for demand-start services and not automatic evidence of a fault.
4. Show the five fixed SFC/DISM plans with exact allowlisted arguments, a conspicuous distinction between inspection and modification, network considerations and safety notes.
5. Inspect the selected plan only when requested, map failures conservatively and never show a prior preflight for a different selection.
6. Do not add ANY execute, repair, reset, disable-service, reboot, shell-launch or administrator-elevation command.
7. No log/hostname/serial/private path leakage. Do not expose the preflight \`TrustedToolPath\` in the UI.
8. Preserve v0.2.2 version and published artifacts, \`main\` and open draft PR.

## NON-GOALS

Repairing Windows Update, invoking SFC/DISM, automatically creating restore points, persisting sensitive service logs, admin B2/manual Windows smoke, merging the PR or publishing v0.3.

## DESIGN

- \`RepairCenterViewModel\` composes \`IWindowsUpdateReadinessService\`, \`ISystemRepairPlanService\`, and \`ISystemRepairPreflightService\` via constructor injection; production wiring uses the existing manual-service pattern in \`MainViewModel\`.
- \`RepairCenterView\` is a WPF display-only page with just two commands: read-only service consultation and read-only preflight review.
- All statuses use neutral predefined language. There is no execution service or execution interface in the UI.

## RISKS

- WMI can be slow: perform on worker thread, keep WPF responsive.
- UI could imply that stopped services are broken: explicitly disallow that interpretation.
- Plan selection could show stale status: invalidate preflight text whenever selection changes.
- WPF resource binding errors: add STA view loading/navigation regression tests.
- Source version is still 0.2.2 until the separate release gate; do not bump version for a draft.

## ACCEPTANCE

- No observation/preflight is called on construction or navigation.
- Exactly five fixed plans, all execution unavailable, and clear warning for modifying operations.
- Read-only commands work, sanitize error messages, and clear stale evidence on refresh or selection.
- Both UI navigation and WPF XAML load on STA, 0 error.
- Full Release build and all Windows CI tests for the EXACT development SHA, 0 failure/0 skip.
- Diff limited to repair UI, navigation, tests, this spec. Stable \`main\` and releases unchanged.

## EVIDENCE / DISPOSITION

Record commit, PR #1, CI run/jobs/logs and test count after execution. CI pass establishes source/build gate, not field repair certification. No manual Windows action is required for this slice.
