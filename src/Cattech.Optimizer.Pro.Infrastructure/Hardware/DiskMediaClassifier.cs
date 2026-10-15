using System.Text.RegularExpressions;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Conservative physical disk media classification.
/// Win32_DiskDrive.MediaType = "Fixed hard disk media" does NOT establish
/// whether the underlying device is HDD or SSD. Unknown is preferable to a
/// false HDD diagnosis that triggers an incorrect upgrade recommendation.
/// </summary>
internal static class DiskMediaClassifier
{
    internal const string Unknown = "No determinado";

    public static string Classify(string? model, string? interfaceType, string? wmiMediaType)
    {
        var explicitSsd = HasToken(model, "SSD")
            || Contains(model, "NVMe")
            || Contains(interfaceType, "NVMe")
            || HasToken(wmiMediaType, "SSD")
            || Contains(wmiMediaType, "Solid State");

        var explicitHdd = HasToken(model, "HDD")
            || string.Equals(wmiMediaType?.Trim(), "HDD", StringComparison.OrdinalIgnoreCase)
            || Contains(wmiMediaType, "Rotating");

        // Contradictory information must never result in a confident recommendation.
        if (explicitSsd && explicitHdd)
            return Unknown;
        if (explicitSsd)
            return "SSD";
        if (explicitHdd)
            return "HDD";

        // eMMC is flash-based; avoid mislabeling it as an ordinary SATA/NVMe SSD.
        if (Contains(model, "eMMC") || Contains(interfaceType, "eMMC"))
            return "Flash";

        return Unknown;
    }

    private static bool Contains(string? text, string fragment) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    // Require whole tokens for HDD and SSD; words like "SHDD" must not match.
    private static bool HasToken(string? text, string token) =>
        !string.IsNullOrWhiteSpace(text)
        && Regex.IsMatch(text,
            @"(?<![A-Za-z0-9])" + Regex.Escape(token) + @"(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
}
