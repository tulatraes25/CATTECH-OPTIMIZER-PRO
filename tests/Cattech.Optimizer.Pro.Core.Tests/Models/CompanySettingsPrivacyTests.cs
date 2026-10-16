using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class CompanySettingsPrivacyTests
{
    private const string PrivatePath =
        @"C:\Users\SyntheticClient\Documents\PRIVATE_SETTINGS_PATH";

    [Fact]
    public async Task FailedSettingsLoad_DoesNotShowExceptionPathOrClaimSuccess()
    {
        var fake = new ThrowingSettingsService { LoadFailure = new IOException(PrivatePath) };
        var vm = new CompanySettingsViewModel(fake);

        await vm.LoadAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.DoesNotContain("PRIVATE_SETTINGS_PATH", vm.ErrorMessage);
        Assert.DoesNotContain(@"C:\Users", vm.ErrorMessage);
        Assert.Contains("configuración", vm.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedSettingsSave_KeepsFormDirty_WithoutPrivatePaths()
    {
        var fake = new ThrowingSettingsService { SaveFailure = new IOException(PrivatePath) };
        var vm = new CompanySettingsViewModel(fake)
        {
            CompanyName = "Synthetic Shop",
            TechnicianName = "Synthetic Technician"
        };
        Assert.True(vm.HasUnsavedChanges);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.True(vm.HasUnsavedChanges);
        Assert.DoesNotContain("PRIVATE_SETTINGS_PATH", vm.ErrorMessage);
        Assert.Contains("sin guardar", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuccessfulSettingsSave_StillClearsDirtyState()
    {
        var fake = new ThrowingSettingsService();
        var vm = new CompanySettingsViewModel(fake)
        {
            CompanyName = "Synthetic Shop",
            TechnicianName = "Synthetic Technician"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.True(vm.IsSuccess);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(1, fake.Saves);
    }

    private sealed class ThrowingSettingsService : ISettingsService
    {
        public Exception? LoadFailure { get; set; }
        public Exception? SaveFailure { get; set; }
        public int Saves { get; private set; }
        public AppSettings CurrentSettings { get; } = new();

        public Task<AppSettings> LoadSettingsAsync()
        {
            if (LoadFailure is not null)
                throw LoadFailure;
            return Task.FromResult(CurrentSettings);
        }

        public Task SaveSettingsAsync(AppSettings settings)
        {
            if (SaveFailure is not null)
                throw SaveFailure;
            Saves++;
            return Task.CompletedTask;
        }

        public event EventHandler<AppSettings>? SettingsChanged
        {
            add { }
            remove { }
        }
    }
}
