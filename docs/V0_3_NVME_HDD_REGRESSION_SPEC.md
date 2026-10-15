# SPEC Kiro - Storage type / false HDD migration recommendation

## GOAL

Correct the observed focal smoke finding \`SMOKE-V022F-001\`: a model named \`NVMe WD PC SN560...\` was reported as HDD by CATTECH and prompted an HDD-to-SSD migration recommendation.

## ROOT CAUSE (source analysis)

Before this change, \`WmiHardwareService.GetDiskInfoAsync()\` used \`Win32_DiskDrive.MediaType.Contains("SSD") ? "SSD" : "HDD"\`. On Windows the generic value \`Fixed hard disk media\` does **not** prove rotational storage. \`DiagnosticService.GenerateAlerts\` additionally suggested migration for the default \`No detectado\` state.

## REQUIREMENTS

1. Pure deterministic classification independent from live WMI, with tests for NVMe WD PC SN560, SSD models, explicit HDD, generic "Fixed hard disk media", unknown and conflicting evidence.
2. Never infer HDD from the absence of an SSD token.
3. Only offer an HDD-to-SSD migration suggestion when the *classified* disk type is explicitly \`HDD\`, not on unknown/empty/compound values.
4. Do not use \`InterfaceType=SATA/SCSI/USB\` to infer rotational media. \`NVMe\` is positive solid-state evidence in the currently supported device context.
5. Conflicting or insufficient evidence => \`No determinado\`, never a confident recommendation.
6. Preserve actual SMART checks and other diagnostic alerts. Do not add privileged probes or require external tools.
7. Preserve v0.2.2 main/release and manual focal evidence. Fix in development source first.

## NON-GOALS / RISK

- Do not claim every mechanical disk can now be positively identified. Generic WMI properties do not prove the media type.
- Do not guess per-partition free space or blindly link \`Win32_LogicalDisk\` to \`Win32_DiskDrive\`; that association is an independent issue.
- Do not certify the fix on the official v0.2.2 binary, or claim hardware verification, from green CI. Require future real-system focal acceptance.
- A future better detection pass may correlate Storage Management \`MSFT_PhysicalDisk.MediaType\` to the exact physical device using stable identifiers; never pair by enumeration order.

## ACCEPTANCE

- NVMe WD PC SN560 with generic WMI media label => SSD, no HDD upgrade suggestion.
- Unknown => \`No determinado\`, no HDD upgrade suggestion.
- Explicit HDD => HDD and suggestion.
- Contradiction => unknown; no suggestion.
- Full Windows CI build, all tests 0 failed/0 skipped, exact SHA, diff check/review, no release/main mutation.

## EVIDENCE

Record commit, current PR, exact Windows CI run and regression test totals. Treat source bug as fixed in development only; released v0.2.2 remains historically unchanged.
