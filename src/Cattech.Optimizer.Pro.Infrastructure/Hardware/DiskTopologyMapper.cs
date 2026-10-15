using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Maps only WMI-proven disk -> partition -> logical volume relationships.
/// Never pairs physical disks and drive letters by enumeration order.
/// </summary>
internal static class DiskTopologyMapper
{
    internal static List<DiskInfo> Map(
        IReadOnlyList<PhysicalDiskRecord> physicalDisks, string? systemVolumeLetter)
    {
        var system = NormalizeDriveLetter(systemVolumeLetter);
        // If the same logical volume appears on multiple physical disks,
        // a unique physical owner has not been established (dynamic/spanned
        // disks, storage virtualization or corrupt WMI mapping).
        var owners = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < physicalDisks.Count; i++)
        {
            if (!physicalDisks[i].AssociationsAvailable)
                continue;

            foreach (var raw in physicalDisks[i].Volumes)
            {
                var letter = NormalizeDriveLetter(raw.DriveLetter);
                if (letter == null)
                    continue;

                if (!owners.TryGetValue(letter, out var ownersForDrive))
                {
                    ownersForDrive = new HashSet<int>();
                    owners.Add(letter, ownersForDrive);
                }
                ownersForDrive.Add(i);
            }
        }

        var results = new List<DiskInfo>(physicalDisks.Count);
        for (var i = 0; i < physicalDisks.Count; i++)
        {
            var raw = physicalDisks[i];
            var mapped = new List<DiskVolumeInfo>();
            if (raw.AssociationsAvailable)
            {
                var candidates = raw.Volumes
                    .Select(volume => (Volume: volume,
                        Letter: NormalizeDriveLetter(volume.DriveLetter)))
                    .Where(entry => entry.Letter != null)
                    .GroupBy(entry => entry.Letter!, StringComparer.OrdinalIgnoreCase);

                foreach (var group in candidates)
                {
                    // Duplicated or multi-disk association is ambiguous.
                    if (group.Count() != 1 ||
                        !owners.TryGetValue(group.Key, out var ownerSet) ||
                        ownerSet.Count != 1)
                        continue;

                    var volume = group.Single().Volume;
                    mapped.Add(new DiskVolumeInfo
                    {
                        DriveLetter = group.Key,
                        CapacityBytes = volume.CapacityBytes,
                        FreeBytes = volume.FreeBytes,
                        IsSystemVolume = string.Equals(
                            group.Key, system, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            mapped.Sort((x, y) => string.Compare(x.DriveLetter, y.DriveLetter,
                StringComparison.OrdinalIgnoreCase));
            var selectedSystem = mapped.FirstOrDefault(x => x.IsSystemVolume);

            results.Add(new DiskInfo
            {
                Name = raw.Name,
                Interface = raw.Interface,
                MediaType = DiskMediaClassifier.Classify(
                    raw.Name, raw.Interface, raw.MediaType),
                TotalGB = raw.CapacityBytes.HasValue
                    ? Math.Round(raw.CapacityBytes.Value / (1024d * 1024 * 1024), 2)
                    : 0,
                VolumeAssociationsVerified = raw.AssociationsAvailable,
                Volumes = mapped,
                DriveLetter = string.Join(", ", mapped.Select(x => x.DriveLetter)),
                // Kept for old consumers, never used as evidence without
                // HasVerifiedSystemVolumeMetrics.
                FreeGB = selectedSystem?.FreeGB ?? 0
            });
        }

        return results;
    }

    internal static string? NormalizeDriveLetter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();
        // Allow C: or C:\ only. No volume GUID paths, device paths,
        // arbitrary directory strings or fuzzy prefix matching.
        if (value.Length == 3 && (value[2] == '\\' || value[2] == '/'))
            value = value[..2];

        return value.Length == 2
            && char.IsLetter(value[0])
            && value[0] <= 127
            && value[1] == ':'
            ? char.ToUpperInvariant(value[0]) + ":"
            : null;
    }
}
