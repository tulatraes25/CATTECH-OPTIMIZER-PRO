using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Infrastructure.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Disposable synthetic audit JSON only; never opens a real cleanup history.
/// </summary>
public class CleanupHistoryIntegrityTests
{
    private static readonly DateTime Started =
        new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LatestCorruptHistory_CannotHideEarlierValidAuditAtResultLimit()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-cleanup-list-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TempCleanupService(root);
            var record = new TempCleanupResult
            {
                Id = "ABC12345",
                StartedAt = Started,
                FinishedAt = Started.AddMinutes(1),
                DeletedFiles = 3,
                DeletedBytes = 150
            };
            var filename = await service.SaveResultAsync(record);
            var dir = Path.Combine(root, "data", "cleanup-results");
            var valid = Path.Combine(dir, filename);
            var savedBefore = await File.ReadAllTextAsync(valid);
            File.SetLastWriteTimeUtc(valid, DateTime.UtcNow.AddMinutes(-5));
            var corrupt = Path.Combine(dir, "cleanup-result-corrupt.json");
            await File.WriteAllTextAsync(corrupt, "{ malformed");
            File.SetLastWriteTimeUtc(corrupt, DateTime.UtcNow);

            var list = await service.ListResultsAsync(1);
            var summary = Assert.Single(list);

            Assert.Equal("ABC12345", summary.Id);
            Assert.Equal(filename, summary.FileName);
            Assert.Equal(3, summary.DeletedFiles);
            Assert.Equal(savedBefore, await File.ReadAllTextAsync(valid));
            Assert.True(File.Exists(corrupt));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":\"A\"}")]
    [InlineData("{\"id\":null,\"startedAt\":\"2026-10-15T10:00:00Z\",\"finishedAt\":\"2026-10-15T10:01:00Z\"}")]
    [InlineData("{\"id\":\"A\",\"ID\":\"B\",\"startedAt\":\"2026-10-15T10:00:00Z\",\"finishedAt\":\"2026-10-15T10:01:00Z\"}")]
    [InlineData("{\"id\":\"A\",\"startedAt\":\"2026-10-15T10:00:00Z\",\"finishedAt\":\"2026-10-15T09:00:00Z\"}")]
    [InlineData("{\"id\":\"A\",\"startedAt\":\"2026-10-15T10:00:00Z\",\"finishedAt\":\"2026-10-15T10:01:00Z\",\"deletedBytes\":-1}")]
    public async Task InvalidAudit_DoesNotInventHistoryOrModifyOriginal(string invalid)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-invalid-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dir = Path.Combine(root, "data", "cleanup-results");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "cleanup-result-synthetic.json");
            await File.WriteAllTextAsync(file, invalid);

            var list = await new TempCleanupService(root).ListResultsAsync();
            Assert.Empty(list);
            Assert.Equal(invalid, await File.ReadAllTextAsync(file));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NonPositiveListLimit_ReturnsEmptyWithoutChangingAudit()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-history-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TempCleanupService(root);
            var filename = await service.SaveResultAsync(new TempCleanupResult
            {
                StartedAt = Started,
                FinishedAt = Started.AddMinutes(1)
            });
            Assert.Empty(await service.ListResultsAsync(0));
            Assert.Empty(await service.ListResultsAsync(-5));
            Assert.True(File.Exists(Path.Combine(root, "data", "cleanup-results", filename)));
            Assert.Single(await service.ListResultsAsync(1));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
