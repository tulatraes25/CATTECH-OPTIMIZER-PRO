using System.Xml.Linq;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Static WPF resource-contract tests. Visual keyboard/screen-reader QA
/// on real Windows is a separate release gate.
/// </summary>
public sealed class ReportClientSelectionXamlTests
{
    [Fact]
    public void ClientPicker_ShowsClientEquipmentAndServiceDate_NotJustClientName()
    {
        var xml = LoadReportViewXaml();
        var picker = FindClientPicker(xml);

        Assert.Equal("{Binding SelectedServiceReport}",
            picker.Attribute("SelectedItem")?.Value);
        Assert.Equal("{Binding HasClientData}",
            picker.Attribute("IsEnabled")?.Value);
        Assert.Null(picker.Attribute("DisplayMemberPath"));

        var template = Assert.Single(picker.Elements()
            .Where(e => e.Name.LocalName == "ComboBox.ItemTemplate"));
        var bindings = template.Descendants()
            .Where(e => e.Name.LocalName == "TextBlock")
            .Select(e => e.Attribute("Text")?.Value ?? string.Empty)
            .ToList();

        Assert.Contains("{Binding Client.Name}", bindings);
        Assert.Contains("{Binding Equipment.Brand}", bindings);
        Assert.Contains("{Binding Equipment.Model}", bindings);
        Assert.Contains(bindings, v => v.Contains("CreatedAt",
            StringComparison.Ordinal));
        Assert.DoesNotContain(bindings, v => v.Contains("SerialNumber",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(bindings, v => v.Contains("Client.Phone",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientPicker_RemainsBoundToAvailableReports_NotANameOnlyString()
    {
        var xml = LoadReportViewXaml();
        var picker = FindClientPicker(xml);
        Assert.Equal("{Binding AvailableServiceReports}",
            picker.Attribute("ItemsSource")?.Value);
        Assert.Contains(picker.Descendants(), e =>
            e.Name.LocalName == "DataTemplate");

        // Inventory-integrity warnings remain in the view; UI shall not
        // silently hide a corrupted/ambiguous customer report store.
        Assert.Contains(xml.Descendants(), e =>
            (e.Attribute("Visibility")?.Value ?? string.Empty)
                .Contains("HasReportInventoryWarning", StringComparison.Ordinal));
    }

    private static XElement FindClientPicker(XDocument document) =>
        Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "ComboBox"
            && (element.Attribute("ItemsSource")?.Value ?? string.Empty)
                .Contains("AvailableServiceReports", StringComparison.Ordinal)));

    private static XDocument LoadReportViewXaml()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.UI/Views/ReportView.xaml"));
        return XDocument.Load(path);
    }
}
