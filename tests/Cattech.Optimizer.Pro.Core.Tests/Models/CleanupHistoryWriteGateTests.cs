using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Infrastructure.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class CleanupHistoryWriteGateTests
{
    private static readonly DateTime Start =
        new(2026, 10, 15, 11, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("blank-id")]
    [InlineData("missing-start")]
    [InlineData("missing-finish")]
    [InlineData("backwards-time")]
    [InlineData("negative-count")]
    [InlineData("negative-bytes")]
    [InlineData("negative-selected-bytes")]
    [InlineData("null-errors")]
    [InlineData("null-targets")]
    [InlineData("null-error-entry")]
    [InlineData("null-target-entry")]
    public async Task InvalidNewAudit_MustNotCreateAnyDirectoryOrFile(string defect)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-audit-writegate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TempCleanupService(root);
            var record = ValidRecord();
            switch (defect)
            {
                case "blank-id": record.Id = " "; break;
                case "missing-start": record.StartedAt = default; break;
                case "missing-finish": record.FinishedAt = default; break;
                case "backwards-time": record.FinishedAt = Start.AddSeconds(-1); break;
                case "negative-count": record.DeletedFiles = -1; break;
                case "negative-bytes": record.DeletedBytes = -1; break;
                case "negative-selected-bytes": record.TotalSelectedBytes = -1; break;
                case "null-errors": record.Errors = null!; break;
                case "null-targets": record.TargetResults = null!; break;
                case "null-error-entry": record.Errors.Add(null!); break;
                case "null-target-entry": record.TargetResults.Add(null!); break;
            }

            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveResultAsync(record));
            Assert.False(Directory.Exists(root));
            Assert.Empty(await service.ListResultsAsync());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidNextAudit_NeverPoisonsExistingValidAudit()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-audit-preserve-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TempCleanupService(root);
            var filename = await service.SaveResultAsync(ValidRecord());
            var dir = Path.Combine(root, "data", "cleanup-results");
            var path = Path.Combine(dir, filename);
            var saved = await File.ReadAllTextAsync(path);

            var invalid = ValidRecord();
            invalid.Errors.Add(null!);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveResultAsync(invalid));

            Assert.Equal(saved, await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(dir, "cleanup-result-*.json"));
            Assert.Empty(Directory.GetFiles(dir, ".cattech-cleanup-*.tmp"));
            var history = Assert.Single(await service.ListResultsAsync());
            Assert.Equal(filename, history.FileName);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static TempCleanupResult ValidRecord() => new()
    {
        Id = "SYNTHETIC-001",
        StartedAt = Start,
        FinishedAt = Start.AddSeconds(2),
        DeletedFiles = 2,
        DeletedBytes = 1024,
        TotalSelectedBytes = 2048,
        TechnicianName = "Synthetic Technician"
    };
}
