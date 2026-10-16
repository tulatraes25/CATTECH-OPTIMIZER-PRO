# SPEC Kiro — CI warning triage (October 2026)

## Evidence before this slice
Windows CI #166 on source
`251d0436a53e2ff789b1df0fceb3357e81bd01e8`:
Release build PASS, xUnit 1545/0/0. Its raw job log contained
**384 warning lines** by warning code:

| Diagnostic | Logged lines | Source |
|---|---:|---|
| CA1416 | 314 | Windows-only registry/platform calls in Infrastructure |
| CS8625 | 68 | Test invocation of WPF IValueConverter with null arguments |
| CS0067 | 2 | Intentionally unused SettingsChanged event in a test fake |

Counts include repeated warning emissions across build/test jobs;
they are NOT 384 distinct source defects.

## Goal
Improve nullability contracts and suppress no real diagnostic;
keep all behavior, security and runtime support unchanged.

## Change
Five converter `Convert` methods now accept nullable value
and parameter, matching actual WPF binding behavior and existing
tests that deliberately pass null. The null/string converter
retains its N/D default. The immutable settings test fake now
uses explicit no-op event accessors, stating that it never fires.

No `#pragma warning disable`, `NoWarn`, project-wide nullable
relaxation, Windows support claim, SDK upgrade or registry mutation.
No converter other than these five is changed.

## Acceptance
Full Windows Restore, Release build and xUnit 0 fail/skip at
the exact new SHA; warning code counts reviewed, with no added
CA1416 or new nullable regression. CS8625 and CS0067 should
drop without changing the expected converter outputs.

## Residual
CA1416 represents real Windows-platform reachability analysis
and needs targeted guarded entrypoints or explicit Windows-only
API boundaries. Do not silence it globally; hardware/platform
QA and public release remain separate gates.

Status: DRAFT / exact new SHA CI pending.
