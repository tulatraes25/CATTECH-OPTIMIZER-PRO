# GitHub source-of-truth and certification gates

## Snapshot

1. Read `/branches/main`, selected development branch, open PR details, release/tag objects, current CI runs and head SHA. Use GitHub connector actions and report their returned object IDs.
2. Read exact source files/diffs on the current SHA; read `ROADMAP.md`, relevant `docs/`, issue acceptance criteria, `.github/workflows`, test project and repository-local guidance.
3. If the branch has advanced unexpectedly, inspect new commits and compare against known checkpoint before making changes. Never replay an old work plan on a new head without re-evaluation.

## Branch-based development

- Existing pattern: `main` stable; `develop/v0.3` and PR draft `#1` are historical known names, not guaranteed current states. Verify live before reuse.
- Do not use the Release workflow for development. A draft PR targeted to `main` can exercise `pull_request` CI without merging.
- Use small commits named `feat(scope): ...`, `fix(scope): ...`, `test(scope): ...`, `docs(scope): ...`, `chore(scope): ...`.
- Before updating any branch ref, check latest SHA and apply non-force fast-forward with expected previous SHA; if rejected, re-fetch and reconcile.

## Every slice must pass

- Source review: only intended files; no secrets, private logs/hostnames, compiled packages, PII or mojibake.
- Compilation and test evidence: `dotnet restore`, Release build, full tests with 0 failures/0 skips, and focused tests for changed behavior.
- PR CI: a Windows Actions run must target exact head SHA, conclude success and show test totals in job logs. Allow asynchronous in-progress status.
- Security: review untrusted paths, subprocess args, timeouts, cancellation, filesystem identity, privilege escalation, error handling and logging. No executable content accepted merely because unit tests pass.
- Release: separate candidate dry run, immutable source SHA, exact ProductVersion/FileVersion and assets digest, provenance, annotated tag and post-publication verification. Do not automatically publish on a generic "continue".

## Trust distinctions

`GitHub commit exists` != `build success` != `tests pass` != `CI verified` != `QA hardware certified` != `release published`.

A safety check near use time reduces some risk; it does not eliminate a time-of-check/time-of-use race if file identity can change between validation and open. State residual risks plainly.

## Forbidden shortcuts

- No bulk replacement of historical version strings.
- No rewriting history or force push without distinct authorization.
- No silent change to stable `main`, release tags or assets during active development.
- No closed findings solely from theoretical assertions or self-reported tests; inspect committed code and run logs.
