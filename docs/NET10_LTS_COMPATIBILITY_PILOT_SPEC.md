# CATTECH v0.3 — SPEC Kiro: .NET 10 LTS compatibility pilot

**Status:** EXPERIMENT / CI-ONLY / NOT A RELEASE. No `main` merge or public binary publication.

## GOAL
Qualify `.NET 10 LTS` before public v0.3 release, keeping the known-good .NET 8 source available for rollback. Microsoft's official support policy ends .NET 8 support on 2026-11-10; .NET 10 LTS remains supported through 2028.

Sources: https://dotnet.microsoft.com/en-us/platform/support/policy and https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/ .

## BASELINE AND SCOPE
- CI-green source base `0a7fe181f38f4cab32aa04527fbf7334eb019f36` (integration PR #77, Windows CI #220 1650/0/0).
- Four project frameworks: `net10.0` for portable Core, `net10.0-windows` for Windows Infrastructure, WPF UI and xUnit tests.
- `setup-dotnet` in CI and *candidate packaging* workflows changed to `10.0.x`.
- All project package versions intentionally retained for a compatibility smoke first. Resolve incompatible packages only based on exact errors, not blind updates.
- Visible app version remains **v0.2.2** in this *experimental source*; never publish from this branch or claim a v0.3 candidate package. Upgrade the version coherently with release notes only after migration is accepted.

## NON-GOALS
No modification of `main`, v0.2.2 tag/assets, service report data, Windows services, admin privileges, installer, real SMART/WMI hardware, or destructive repairs. No merge of this branch just to make a test green.

## ACCEPTANCE GATES
1. Exact branch SHA readback and small diff limited to four project TFMs, two workflow SDK selectors, and this SPEC.
2. Windows GitHub Actions Restore, Release WPF Build and full xUnit: zero failures and zero skips. Inspect warning log and compare with .NET 8 base CI #220.
3. Verify test infrastructure runs actual `net10.0-windows` assemblies, not stale `net8.0` artifacts; collect one independent Cobertura report per assembly.
4. Additional *future* release-candidate dry-run: win-x64 self-contained publish, ZIP file contents, hashes, ProductVersion/FileVersion, provenance and two test runs from an exact version-consistent candidate commit.
5. **Physical** Windows 10 (including documented minimum version) and Windows 11 smoke: WPF screen loading, WMI/SMBIOS, DXGI GPU mapping, SMART external smartctl, sensor drivers, BSOD DumpChk, Edge PDF, read-only repair preflight, permission handling and path safety. No destructive cleanup or Windows repair without separate consent.
6. Document compatibility/rollback findings and either integrate into versioned v0.3 development only after acceptance or keep .NET 8 until a supported alternative passes.

## RISK / STOP
An unupgraded NuGet dependency, Windows WPF targeting limitation, or packaging mismatch is a **BLOCKER** to migration, not a reason to suppress tests. CI success is not physical or shipping certification. This pilot branch has an independent CI only; the stable `main` and integration branch remain unchanged.
