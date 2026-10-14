using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ReportIndexConcurrencyTests
{

    [Theory]
    [InlineData("{not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("")]
    public async Task InvalidIndex_FailsClosed_WithoutReplacingContents(string invalidJson)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-index-corrupt-" + Guid.NewGuid().ToString("N"));
        var indexDir = Path.Combine(root, "reports", "html");
        Directory.CreateDirectory(indexDir);
        var indexPath = Path.Combine(indexDir, "reports-info.json");
        await File.WriteAllTextAsync(indexPath, invalidJson);
        try
        {
            var service = new HtmlReportService(root);
            var error = await Record.ExceptionAsync(() =>
                service.SaveReportInfoAsync(new GeneratedReportInfo
                {
                    ClientName = "Synthetic QA",
                    HtmlPath = "test-new.html"
                }));
            Assert.True(error is JsonException or InvalidDataException,
                $"Unexpected status: {error?.GetType().Name ?? "none"}");

            Assert.Equal(invalidJson, await File.ReadAllTextAsync(indexPath));
            Assert.Empty(Directory.GetFiles(indexDir, ".cattech-index-*.tmp"));

            var listError = await Record.ExceptionAsync(
                () => service.ListGeneratedReportsAsync());
            Assert.True(listError is JsonException or InvalidDataException);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
    [Fact]
    public async Task ConcurrentWritersAcrossInstances_RetainEveryMetadataEntry()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-index-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const int count = 80;
            var jobs = Enumerable.Range(0, count).Select(async i =>
            {
                var service = new HtmlReportService(root);
                await service.SaveReportInfoAsync(new GeneratedReportInfo
                {
                    Id = $"QA{i:D4}",
                    ClientName = $"Synthetic Client {i}",
                    HtmlPath = $"test-{i:D4}.html"
                });
            });

            await Task.WhenAll(jobs);
            var path = Path.Combine(root, "reports", "html", "reports-info.json");
            var json = await File.ReadAllTextAsync(path);
            var entries = JsonSerializer.Deserialize<List<GeneratedReportInfo>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(entries);
            Assert.Equal(count, entries.Count);
            Assert.Equal(count, entries.Select(x => x.Id).Distinct().Count());
            Assert.Equal(count, entries.Select(x => x.HtmlPath).Distinct().Count());
            Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(path)!),
                file => Path.GetFileName(file).StartsWith(".cattech-index-",
                    StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RepeatedWrites_KeepValidJsonAndNoStagingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-index-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new HtmlReportService(root);
            for (var i = 0; i < 10; i++)
            {
                await service.SaveReportInfoAsync(new GeneratedReportInfo
                {
                    ClientName = "Synthetic",
                    HtmlPath = $"synthetic-{i}.html"
                });
                var indexDir = Path.Combine(root, "reports", "html");
                var json = await File.ReadAllTextAsync(
                    Path.Combine(indexDir, "reports-info.json"));
                using var parsed = JsonDocument.Parse(json);
                Assert.Equal(i + 1, parsed.RootElement.GetArrayLength());
                Assert.Empty(Directory.GetFiles(indexDir, ".cattech-index-*.tmp"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
