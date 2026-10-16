using System.Text.Json.Nodes;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Synthetic, disposable files only. No real customer stores are read/deleted.
/// </summary>
public class ServiceReportStrictReadTests
{
    [Theory]
    [InlineData("client", "null")]
    [InlineData("equipment", "null")]
    [InlineData("service", "null")]
    [InlineData("client", "[]")]
    [InlineData("equipment", "\"not an object\"")]
    [InlineData("service", "[]")]
    public async Task RequiredSectionWrongType_IsInvalidAndNeverLoadedOrDeleted(
        string section, string replacement)
    {
        await WithSyntheticReportAsync(async (service, report, path) =>
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            document[section] = JsonNode.Parse(replacement);
            await AssertBadRecordPreservedAsync(service, report.Id, path,
                document.ToJsonString());
        });
    }

    [Theory]
    [InlineData("client")]
    [InlineData("equipment")]
    [InlineData("service")]
    [InlineData("id")]
    public async Task MissingRequiredSection_CannotBeSilentlyDefaulted(string section)
    {
        await WithSyntheticReportAsync(async (service, report, path) =>
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            Assert.True(document.Remove(section));
            await AssertBadRecordPreservedAsync(service, report.Id, path,
                document.ToJsonString());
        });
    }

    [Theory]
    [InlineData("client")]
    [InlineData("equipment")]
    [InlineData("service")]
    [InlineData("id")]
    public async Task DuplicateRequiredProperty_RejectsEvenWhenLastCopyLooksValid(string section)
    {
        await WithSyntheticReportAsync(async (service, report, path) =>
        {
            var json = await File.ReadAllTextAsync(path);
            var marker = "\"" + section + "\": ";
            Assert.Contains(marker, json);
            var injected = "\"" + section + "\": " + (section == "id" ? "\"shadow\", " : "{}, ");
            var bad = json.Replace(marker, injected + marker, StringComparison.Ordinal);
            await AssertBadRecordPreservedAsync(service, report.Id, path, bad);
        });
    }

    [Theory]
    [InlineData("actionsPerformed", "null")]
    [InlineData("recommendations", "null")]
    [InlineData("actionsPerformed", "[null]")]
    [InlineData("recommendations", "[17]")]
    public async Task MalformedServiceList_RejectsInsteadOfCreatingUnsafeNestedValues(
        string property, string replacement)
    {
        await WithSyntheticReportAsync(async (service, report, path) =>
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            document["service"]!.AsObject()[property] = JsonNode.Parse(replacement);
            await AssertBadRecordPreservedAsync(service, report.Id, path,
                document.ToJsonString());
        });
    }

    [Fact]
    public async Task ValidLegacyShortId_AndOmittedOptionalLists_RemainReadable()
    {
        await WithSyntheticReportAsync(async (service, report, path) =>
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            document["id"] = "DEADBEEF";
            var serviceSection = document["service"]!.AsObject();
            Assert.True(serviceSection.Remove("actionsPerformed"));
            Assert.True(serviceSection.Remove("recommendations"));
            await File.WriteAllTextAsync(path, document.ToJsonString());

            var health = await service.InspectReportInventoryAsync();
            Assert.Equal(1, health.UniqueEntries);
            Assert.Equal(0, health.InvalidEntries);
            Assert.False(health.RequiresReview);
            var loaded = await service.LoadReportAsync("DEADBEEF");
            Assert.NotNull(loaded);
            Assert.Empty(loaded.Service.ActionsPerformed);
            Assert.Empty(loaded.Service.Recommendations);
            Assert.Single(await service.ListReportsAsync());
        });
    }

    [Fact]
    public async Task MalformedUnrelatedFile_BlocksReadingAnotherClientAndPreservesBoth()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-report-strict-pair-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonServiceReportService(root);
            var valid = new ServiceReport { Client = new ClientInfo { Name = "Synthetic Good" } };
            var malformed = new ServiceReport { Client = new ClientInfo { Name = "Synthetic Bad" } };
            var validFile = await store.SaveReportAsync(valid);
            var malformedFile = await store.SaveReportAsync(malformed);
            var dir = Path.Combine(root, "data", "service-reports");
            var damagedPath = Path.Combine(dir, malformedFile);
            var node = JsonNode.Parse(await File.ReadAllTextAsync(damagedPath))!.AsObject();
            node["service"] = null;
            await File.WriteAllTextAsync(damagedPath, node.ToJsonString());

            var health = await store.InspectReportInventoryAsync();
            Assert.Equal(2, health.TotalFiles);
            Assert.Equal(1, health.UniqueEntries);
            Assert.Equal(1, health.InvalidEntries);
            Assert.True(health.RequiresReview);
            Assert.Null(await store.LoadReportAsync(valid.Id));
            Assert.False(await store.DeleteReportAsync(valid.Id));
            Assert.True(File.Exists(Path.Combine(dir, validFile)));
            Assert.True(File.Exists(damagedPath));
            Assert.Single(await store.ListReportsAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task AssertBadRecordPreservedAsync(
        JsonServiceReportService store, string id, string path, string corruptedJson)
    {
        await File.WriteAllTextAsync(path, corruptedJson);
        var inventory = await store.InspectReportInventoryAsync();
        Assert.Equal(1, inventory.TotalFiles);
        Assert.Equal(1, inventory.InvalidEntries);
        Assert.Equal(0, inventory.UniqueEntries);
        Assert.True(inventory.RequiresReview);
        Assert.Empty(await store.ListReportsAsync());
        Assert.Null(await store.LoadReportAsync(id));
        Assert.False(await store.DeleteReportAsync(id));
        Assert.Equal(corruptedJson, await File.ReadAllTextAsync(path));
    }

    private static async Task WithSyntheticReportAsync(
        Func<JsonServiceReportService, ServiceReport, string, Task> assertion)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-report-strict-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonServiceReportService(root);
            var report = new ServiceReport { Client = new ClientInfo { Name = "Synthetic QA" } };
            var name = await store.SaveReportAsync(report);
            var path = Path.Combine(root, "data", "service-reports", name);
            await assertion(store, report, path);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
