# CATTECH response and OpenCode handoff format

## User-visible report

Keep the response compact and Spanish-friendly:

1. `Estado verificado`: repo, active branch and exact SHA, PR, workflow run outcome.
2. `Cambio real`: files/behavior changed or research completed. Distinguish `implemented` from `certified`.
3. `Gate`: focused tests, full tests, build, GitHub CI head SHA and concrete blockers. Never assert tests you did not run or inspect.
4. `Riesgos`: severity, reproducibility, evidence, resolution/issue URL, residual unknowns.
5. `Progreso`: explicit conservative estimates for subphase, v0.3 or current release, and overall phase when meaningful. No precision unsupported by checkpoints.
6. `Siguiente paso`: one safe, atomic slice, or a STOP when a gate failed.

Use links to actual commits, CI and PR. Avoid repeating huge inventories when user says "seguir"; emphasize what moved since the last verified checkpoint.

## Work handoff to OpenCode + DeepSeek v4 Flash

If connected GitHub write tools cannot complete the change, or local Windows execution is required, supply one copyable self-contained prompt with:

- Repository URL, branch, exact baseline SHA, expected clean state.
- SPEC: goal, requirements, non-goals, threat model, files, acceptance criteria.
- Change boundaries; deny implicit refactors and version bumps.
- Commands: restore, Release build, focused and full suite, `git diff --check`; expected counts only as a baseline to confirm, not a mandate to falsify.
- Commit message exact; push only to development branch; wait for PR CI on new SHA; never auto-merge.
- Numbered report: files, commit, test totals, CI, issue states and next step.

OpenCode's report is not proof by itself; use the GitHub connector to re-check SHA, diff, run IDs and logs before accepting.

## Decision on negative outcomes

`BLOCKED`: security or build gate prevents progress. `PENDING`: CI/manual verification not finished. `PARTIAL`: functionality exists but exact acceptance is unproven. `PASS`: all declared acceptance gates supported by evidence. Avoid issuing a new release merely to make a dashboard more green.
