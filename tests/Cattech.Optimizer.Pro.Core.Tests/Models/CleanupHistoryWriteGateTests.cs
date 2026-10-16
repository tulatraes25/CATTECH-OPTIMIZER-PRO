using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Infrastructure.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class CleanupHistoryWriteGateTests
{
    private static readonly DateTime Start = new(2026, 10, 15, 10, 0, 0);

    [Theory]
    [InlineData("blank-id")]
    [InlineData("zero-start")]
    [InlineData("zero-end")]
    [InlineData("backwards")]
    [InlineData("negative-count")]
    [InlineData("negative-bytes")]
    [InlineData("null-errors")]
    [InlineData("null-targets")]
    public async Task InvalidAudit_FailsBeforeCreatingAnyDirectory(string invalidCase)
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-audit-write-" + Guid.NewGuid().ToString("N"));
        try
        {
            var audit = new TempCleanupResult { Id = "synthetic-id", StartedAt = Start, FinishedAt = Start.AddMinutes(1) };
            switch (invalidCase)
            {
                case "blank-id": audit.Id = " "; break;
                case "zero-start": audit.StartedAt = default; break;
                case "zero-end": audit.FinishedAt = default; break;
                case "backwards": audit.FinishedAt = Start.AddMinutes(-1); break;
                case "negative-count": audit.DeletedFiles = -1; break;
                case "negative-bytes": audit.DeletedBytes = -1; break;
                case "null-errors": audit.Errors = null!; break;
                case "null-targets": audit.TargetResults = null!; break;
            }
            var service = new TempCleanupService(root);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveResultAsync(audit));
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidSecondAudit_CannotAlterPreviouslyPublishedResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-audit-preserve-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TempCleanupService(root);
            var first = await service.SaveResultAsync(new TempCleanupResult {
                Id = "synthetic-valid", StartedAt = Start, FinishedAt = Start.AddMinutes(1)
            });
            var path = Path.Combine(root, "data", "cleanup-results", first);
            var before = await File.ReadAllTextAsync(path);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveResultAsync(
                new TempCleanupResult { StartedAt = Start, FinishedAt = Start.AddMinutes(-1) }));
            Assert.Equal(before, await File.ReadAllTextAsync(path));
            Assert.Single(await service.ListResultsAsync());
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "cleanup-result-*.json"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
