# CATTECH — SPEC Kiro QA: first objective coverage baseline

## GOAL
Measure how much code the automated Windows test suite exercises, rather
than misrepresenting "all tests passing" as "100% of product verified".

## CURRENT EVIDENCE AND DESIGN
- .NET 8 / WPF tests already include the `coverlet.collector` package.
  No new third-party dependency or auto-upload to an unrelated service.
- The existing Windows CI performs its normal Restore, Release Build and
  full xUnit suite, now with `--collect:"XPlat Code Coverage"`.
- The job requires exactly one Cobertura XML emitted by the test project.
  It publishes line and branch coverage percentages to GitHub's job summary
  and uploads Cobertura beside its existing TRX artifact for 14 days.
- Coverage percentages are **observations only**; no invented acceptance
  threshold until modules and baselines are assessed. Missing coverage
  data fails the measurement step rather than silently claiming success.

## ACCEPTANCE
1. CI on the exact PR head restores, compiles Release, runs full xUnit
   with 0 failures or skips, and finishes with SUCCESS.
2. The job summary includes parseable Cobertura line and branch rates.
3. The `cattech-test-results` artifact includes TRX and Cobertura XML.
4. No real hardware activity, privileged repair commands, destructive
   cleanup, additional data transfers, new third-party services or
   customer content.
5. Review resulting source coverage distribution before setting a
   per-module minimum; unit coverage does not certify WPF interaction,
   native-device behavior, privacy, WinPE or physical sensor attribution.

## RISKS / NON-GOALS
Coverage instrumentation can increase runtime and will report very low
percentages for external sensors and WPF views that CI cannot exercise.
This is an honest signal, not a release veto by itself. Do not claim
that the reported percentage equals the percentage of implemented
features or acceptance completed. Do not lower or manipulate the metric
to give a more reassuring dashboard.

## STATUS
DRAFT / code committed; pending exact SHA GitHub Windows CI and artifact
inspection. Previous baseline CI #173: 1569/0/0 and 314 CA1416 warnings.
No merge to main and no release authorized.
