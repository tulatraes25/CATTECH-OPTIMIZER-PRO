# CATTECH v0.3 - GitHub-first integration and OpenCode handoff gate

## GOAL
Finish and certify the GitHub source **before** OpenCode downloads it to a local Windows checkout. This is a technical integration ledger, not an approval to merge or publish.

## EVIDENCE (2026-10-16)
- Stable `main`: `02586cc8dc8f6c83c9242582405291d90c55730c`; preserve unchanged pending release gate.
- Stacked coverage branch PR #74: `ba232bddc3defe1453972d397213d2f80a36e54b`, Windows CI #176 SUCCESS (1569 pass, 0 fail, 0 skipped), two independent Cobertura reports each with 64.65% lines and 60.46% branches. This is test coverage, **not** hardware certification.
- Historical `develop/v0.3` SHA `2241171bf12335bcba38782c844cb667cff28baa` is **196 commits behind** coverage HEAD, not a safe release candidate.
- Cleanup-audit PR #76 HEAD `326b4014d8baf454b086102faf506bf0f496e340` diverges from PR #74, merge base `2f1d3f54bae98ed41aa3e625858ab97b9ccf5f52`. Source review found the later coverage line already includes pre-serialization nullable audit-list guards and strict validated-history checks; branch reconciliation must retain any extra targeted tests and compare semantic behavior, not blindly replace the newer source with the side-branch file.
- P0 #75: main protection/ruleset still an administrative prerequisite for a safe merge; do not imply GitHub write permission grants protection.
- P1 #43: parallel evidence/SMART lineage reconciliation outstanding. Existing CI tests are no substitute for source parity and an integrated exact-SHA CI run.

## REQUIREMENTS / ACCEPTANCE
1. Reconcile all feature/bugfix lineages against this isolated integration branch. For each side PR: identify unique commits/files, classify already-covered vs genuinely missing behavior; preserve all distinct safety guards and regression tests.
2. Every integration slice: code review, focused synthetic regression, full Release Windows CI, test counts, build warnings, exact commit SHA and artifact provenance. Never record PASS before the workflow and logs confirm.
3. Retain fail-closed controls on customer data, report identity, cross-case/cross-disk evidence, cleanup authorization, read-only diagnostics and subprocess execution. Do not run destructive Windows operations in CI.
4. Review unresolved issues #2-#21, #33, #43, #50, #53, #55, #58, #60, #75 by code and observed tests rather than assuming stale issue text describes the newest branch.
5. Before any promotion to `main`: verify branch protection, no uncontrolled stacked merges, reconcile side PRs, completed CI and dependency review, version/release integrity, explicit approval of publication.
6. **Only after source integration and GitHub verification:** provide OpenCode + DeepSeek an exact SHA, clean sync procedure and separate Windows 10/11 interactive/hardware acceptance matrix. OpenCode must not independently invent source changes or substitute a different commit. Physical sensors, WMI, SMART, WinPE, WPF interaction and controlled backup/repair remain real-machine gates.

## NON-GOALS / RISKS
No release, direct merge into main, force push, hardware claims, silent suppression of analyzer warnings or unrestricted filesystem deletion. Treat concurrent branches and unprotected main as active hazards.

## TASKS / DISPOSITION
- [x] Create isolated integration baseline at verified coverage SHA.
- [ ] Compare all relevant side branches for **semantic** changes; integrate missing tests/fixes atomically.
- [ ] Obtain Windows full CI green on **integration** HEAD and inspect real logs.
- [ ] Resolve P0 branch governance and prepare explicit release candidate.
- [ ] Handoff exact certified SHA to OpenCode for local synchronization and physical validation.

**STATUS: INTEGRATION OPEN / NO MERGE / NO RELEASE / NO LOCAL SYNC.**
