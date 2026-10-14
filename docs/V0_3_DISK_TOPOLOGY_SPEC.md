# CATTECH v0.3 — SPEC Kiro: physical disk → partition → logical volume

## GOAL
Resolve [issue #3](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/issues/3) source-level false disk/free-space association. Distinguish physical disk capacity from *mounted system-volume* capacity and free bytes, so an unknown measurement never appears as a confirmed full disk or triggers an emergency warning.

## REQUIREMENTS
1. Query actual WMI relationships: \`Win32_DiskDrive\` -> \`Win32_DiskDriveToDiskPartition\` -> \`Win32_DiskPartition\` -> \`Win32_LogicalDiskToPartition\` -> \`Win32_LogicalDisk\`, via explicit \`ManagementObject.GetRelated\` association-class filters.
2. Do not match by disk enumeration order, device number, model-name substring, capacity similarity or arbitrary first letter.
3. Only accept locally mounted fixed logical disks (\`DriveType=3\`); never assume a removable device or network mount maps to a physical disk.
4. If one letter is associated to multiple physical disks (e.g. spanned/dynamic/Storage Spaces), or appears multiple times on the same disk, the ownership is **ambiguous** and no physical owner is assigned. If a WMI relationship query fails, discard *all partial* associations for that disk.
5. Identify the Windows system volume from the current OS directory root (not by hardcoded C:, WMI enumeration order or drive 0). If no **unique** verified system-volume association exists, report primary disk and free space as unknown.
6. Store volume \`CapacityBytes\` and \`FreeBytes\` independently, with null for absent/invalid metrics. Compute percentages *only within the same logical volume*. A physical disk's \`TotalGB\` is **not** the denominator.
7. Only display "Disco lleno" when capacity is positive, free bytes are explicitly 0 and the system volume association is unique and complete. "Free bytes not reported" != "0 bytes free".
8. Diagnostic recommendations and generated HTML/PDF use the same verified/unknown semantics. For legacy diagnostics without \`DiskFreeSpaceKnown\`, default to unknown rather than inventing certainty.
9. Preserve media classifier fix (NVMe != HDD) and avoid SSD-upgrade advice for unknown media in BOTH quick-diagnosis alerts and report-recommendation engine.
10. No Windows repair, filesystem mutation, elevated process or upload of WMI device keys, serials, usernames, hostnames, drive object paths or private logs.
11. Keep \`main\`, official v0.2.2 release and its immutable assets untouched.

## NON-GOALS
- Win32 WMI association classes may not reliably expose Storage Spaces, network shares, BitLocker virtual disks, mount points without letters, multi-extent volumes, or non-standard storage drivers. The correct outcome is **N/D**, not a guessed mapping.
- SMART/health detection and free-space attribution are independent. This change does not certify SMART or a real disk on the user's laptop.
- Legacy free-space \`double\` fields remain for serialization compatibility; source consumers must consult new \`DiskFreeSpaceKnown\` flag or \`DiskVolumeInfo.FreePercent\` rather than treating a default zero as measurement.
- No migrations of historic JSON. Old reports without the flag intentionally display "N/D" until refreshed with verified data.
- "PrimaryDisk" means disk holding the current Windows system directory volume, not necessarily UEFI boot partition or BIOS bootloader disk.

## DESIGN
- \`WmiDiskTopologyReader\`: internal source-only WMI graph walker. Does not persist object paths, identifiers, serials or raw WMI output.
- \`DiskTopologyMapper\`: deterministic mapper accepting synthetic physical-disk records and a system-volume letter; filters invalid or multi-owner relations; used independently in CI.
- \`DiskInfo.Volumes\`: associated logical fixed volumes, separately exposing per-volume size/free and whether it is the Windows system volume.
- \`DiskInfo.TotalGB\`: physical capacity; \`DiagnosticReport.DiskCapacityGB\`: verified system-volume capacity when known; never mix them.
- \`DiagnosticReport.DiskFreeSpaceKnown\` and \`EquipmentInfo.DiskFreeSpaceKnown\`: explicit proof flags; absent => false on older JSON. HTML renders N/D for unknown, not 0.0GB.
- \`ClientEquipmentViewModel\`: picks uniquely associated system volume; no physical "first disk" or positional assignment.
- \`DiagnosticService\` and \`ReportRecommendationEngine\`: storage alerts and suggestions require appropriate evidence.
- All relations operate in read-only WMI calls; no automatic repair.

## RISKS / LIMITS
1. WMI provider may return incomplete relationships even on healthy systems: prefer N/D, expose lower completeness, do not silently substitute guess.
2. WMI association queries may be slow on large/multi-disk machines; existing hardware retrieval already executes as a background action in UI. Real responsiveness still requires future field validation.
3. Duplicate ownership across multiple physical disks can be real for a spanned volume; ambiguity must be explicit, never force a single owner.
4. Stable v0.2.2 HTML/JSON contracts are unchanged. v0.3 adds fields; previously saved JSON defaults them to unknown.
5. Existing \`DiskInfo.FreeGB\` stays for legacy compatibility and is not evidence without \`HasVerifiedSystemVolumeMetrics\`. Callers should migrate to the typed \`DiskVolumeInfo\` API.
6. WMI drive-letter and storage virtualization vary by hardware; no production certification without physical system tests.

## TEST MATRIX / ACCEPTANCE
- Two physical disks returned in reversed order, Windows on second.
- Two mounted letters on one physical drive.
- Same logical letter mapped to multiple physical drives => no owner.
- Duplicate conflicting letter on same physical disk => no owner.
- Inaccessible associations, no disks, no mounted system volume, USB/enclosure without fixed volume.
- Windows installed on non-C: volume; malformed/GUID/device-root letters rejected.
- Unknown/invalid volume size, missing/free larger than size => N/D; measured free 0 with known positive size => critical.
- Physical capacity 1000 GiB, logical capacity 100 GiB, free 25 GiB => **25%** free, not 2.5%.
- NVMe with generic Win32 media label remains SSD and no misleading HDD migration advice in reports.
- Legacy report false/default flag suppresses "disco lleno".
- Generated HTML known/unknown cases verified.
- Exact-SHA PR Windows CI Release build, 0 failed/0 skipped tests, diff inspection. No merge or release.

## EVIDENCE / DISPOSITION
Maintain issue #3 as pending field acceptance until WMI is exercised on supported Windows configurations, including multi-volume and multi-disk setups. Even with CI green, classification is **source implementation + CI verified**, never "hardware verified" or "fix in official v0.2.2".
