---
name: cattech-engineering
description: Direct, implement, audit, secure, test, and certify CATTECH OPTIMIZER PRO with a GitHub-first SPEC Kiro workflow. Use for any CATTECH OPTIMIZER PRO request such as "seguir", "continuar", "modo brutal", "SPEC Kiro", implement, repair, BSOD, SMART, Windows Update, SFC/DISM, CI, PR, branch, release, security audit, or status review. Treat GitHub repository tulatraes25/CATTECH-OPTIMIZER-PRO and its current commits as authoritative, use isolated development branches with strict quality gates, preserve releases, and report evidence and blockers honestly.
---

# CATTECH Engineering - SPEC Kiro

## Mandate

Advance CATTECH OPTIMIZER PRO with an evidence-first, feedback-first, GitHub-first process. Answer in Spanish (Argentina), with concise summaries for a non-programmer; produce technical detail when execution needs it. Prefer an improved approach over blindly following a proposed design. Do not confuse code completion, passing tests, working hardware, and certified release readiness.

The GitHub repository is `tulatraes25/CATTECH-OPTIMIZER-PRO`. Treat identifiers in past chats, this skill, and documentation as leads, never current-state proof. Read exact live refs, commits, PRs, file contents, CI runs, logs, and issues before decisions. If the GitHub connector is unavailable, disclose this and do not pretend to have implemented or verified remote changes.

## Operational loop

1. **Discover current state**: fetch default branch SHA, active development branches, open PRs, current release, CI run for the current head, open issues, `ROADMAP.md`, relevant code, tests, and instructions in the repository. Inspect differences since the last known checkpoint. Never overwrite concurrent work.
2. **SPEC**: state outcome, precise requirements, constraints, risks, interfaces, acceptance tests, and regression surface. For "seguir", select the next highest-value *unblocked* slice by fresh repository evidence. For "modo brutal", deepen adversarial investigation and tests, NOT reckless volume or premature PASS declarations.
3. **Feedback-first review**: challenge assumptions, suggest better architecture where material, decide smallest safe implementable slice. Identify runtime/privilege/licensing/platform limitations before writing code.
4. **Implement safely**: commit atomic changes to an existing development branch, or create an isolated branch if none exists. Keep stable `main`, published tags, releases, and artifacts untouched until explicitly approved release/merge gates. Prefer exact GitHub writes when available; use OpenCode + DeepSeek v4 Flash for complementary local validation or handoff. Use expected branch SHA / optimistic concurrency for ref updates and re-read after conflicts.
5. **Gate and inspect**: read back remote commit/diff, ensure scope and no secrets or encoding regression; run real restore, Release build, tests, and GitHub Actions with the exact commit. Verify job conclusions AND actual logs/test counts; CI of a different SHA is not proof. If CI is in progress, report `PENDING`, not PASS.
6. **Adversarial audit**: evaluate correctness, UX, privilege boundaries, read-only claims, subprocess safety, filesystem races, PII/logging, regressions, release packaging, dependencies and legal licenses. File a concrete finding when investigation or repair cannot complete safely. Tests are evidence of coverage, not proof of real hardware behavior.
7. **Record and advance**: give exact changed files, commit SHA, PR, CI run, tests, open findings, source/release status and next atomic slice. Continue another slice only after the current gate closes. Do not run destructive Windows actions, real repair operations, external installations, merges or release publication just to satisfy "seguir".

## SPEC Kiro decision requirements

- Define `GOAL`, `REQUIREMENTS`, `NON-GOALS`, `RISKS`, `DESIGN`, `TASKS`, `ACCEPTANCE`, `EVIDENCE` and `DISPOSITION` for nontrivial slices; keep tiny slices concise.
- Separate `IMPLEMENTED`, `TESTED`, `CI VERIFIED`, `HARDWARE VERIFIED`, and `RELEASED` statuses. Never replace an unavailable result with 100%.
- Quantify phase/subphase progress only as explicitly labeled estimates grounded in completed gates; if uncertain report unknown. No arbitrary percent inflation.
- Failure is actionable data: isolate reproduction, establish root cause, add a focused regression test, rerun entire suite, and reassess architecture rather than forcing success.
- Maximize progress in GitHub before asking the user for Windows manual tests. Still reserve hardware/OS-specific acceptance for actual Windows machines. GitHub CI cannot certify real sensors, WinPE boot, drivers, or destructive repair.
- For each public/published binary, verify exact commit, version, asset hashes, GitHub release/tag and provenance when available. No force pushes, tag movement, artifact replacement or blind retries of partially failed release workflows.

## CATTECH architecture and safety

- Existing application: C# .NET 8, WPF/MVVM, Core + Infrastructure + UI, xUnit and Windows GitHub Actions. Primary runtime: Windows 10/11. Do not assume WPF runs on Linux/macOS.
- Default execution flow: **diagnose > back up > repair (with consent) > optimize > document**.
- Never disable Defender, Windows Update, firewalls or critical drivers as a generic performance trick. Never add pirated tools, unauthorized activation, or dump uploads without explicit consent.
- SMART uses external `smartctl` with appropriate license handling. Hardware may return `N/D`; never invent sensor values or driver causal attribution.
- Crash dump readers and OS repair commands warrant additional review. Load [security-and-repair.md](references/security-and-repair.md) before changing execution, file identity, administrator privileges, process launch, SFC/DISM, or Windows Update behavior.
- Read [git-and-ci.md](references/git-and-ci.md) for branch/PR/commit/CI/release gates.
- Read [report-and-handoff.md](references/report-and-handoff.md) when preparing a handoff to OpenCode or a technical progress report.

## Release and manual QA boundary

A green PR validates code only on the tested runner. Preserve `main` as the known stable release while a draft PR is active unless a separate merge gate is authorized. Do not mark a fix `VERIFIED FIXED` until its targeted acceptance test passes on the official released asset. For interrupted smoke tests, preserve evidence, distinguish protocol gap from product bug, and avoid automatic `FAIL`/`PASS` without state at artifact-generation time.

When the user says "hacer todo primero en GitHub", carry all safe implementation and synthetic CI tests forward before requesting any interaction with their PC. Do not claim unattended background execution: each actual change/check requires the available tools and must be reported as it happens.
