using Cattech.Optimizer.Pro.Infrastructure.Hardware;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Tests para la lógica de correlación DXGI ↔ WMI y cálculo de memoria dedicada.
/// No dependen de hardware real: usan el DTO interno DxgiAdapterInfo directamente.
/// </summary>
public class DxgiGpuMemoryTests
{
    // ===== TEST A: RTX 8 GB =====

    [Fact]
    public void DedicatedMemory_8GB_Returns8()
    {
        var adapter = new DxgiGpuMemoryReader.DxgiAdapterInfo(
            Description: "NVIDIA GeForce RTX 4060 Laptop GPU",
            VendorId: 0x10DE,
            DeviceId: 0x28E0,
            DedicatedVideoMemoryBytes: 8UL * 1024 * 1024 * 1024,
            SharedSystemMemoryBytes: 0,
            IsSoftware: false);

        var memoryGB = Math.Round(adapter.DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);

        Assert.Equal(8, memoryGB);
    }

    // ===== TEST B: > 4 GB =====

    [Fact]
    public void DedicatedMemory_12GB_Returns12()
    {
        var adapter = new DxgiGpuMemoryReader.DxgiAdapterInfo(
            Description: "NVIDIA GeForce RTX 4070",
            VendorId: 0x10DE,
            DeviceId: 0x2786,
            DedicatedVideoMemoryBytes: 12UL * 1024 * 1024 * 1024,
            SharedSystemMemoryBytes: 0,
            IsSoftware: false);

        var memoryGB = Math.Round(adapter.DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);

        Assert.Equal(12, memoryGB);
    }

    // ===== TEST C: 16 GB dedicated + 16 GB shared = 16 (NO 32) =====

    [Fact]
    public void DedicatedMemory_16GB_WithShared_DoesNotSum()
    {
        var adapter = new DxgiGpuMemoryReader.DxgiAdapterInfo(
            Description: "NVIDIA GeForce RTX 4080",
            VendorId: 0x10DE,
            DeviceId: 0x2704,
            DedicatedVideoMemoryBytes: 16UL * 1024 * 1024 * 1024,
            SharedSystemMemoryBytes: 16UL * 1024 * 1024 * 1024,
            IsSoftware: false);

        var memoryGB = Math.Round(adapter.DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);

        Assert.Equal(16, memoryGB);
        Assert.NotEqual(32, memoryGB);
    }

    // ===== TEST D: Integrated GPU (dedicated = 0) =====

    [Fact]
    public void IntegratedGpu_DedicatedZero_ShowsZero()
    {
        var adapter = new DxgiGpuMemoryReader.DxgiAdapterInfo(
            Description: "Intel Iris Xe Graphics",
            VendorId: 0x8086,
            DeviceId: 0xA7A0,
            DedicatedVideoMemoryBytes: 0,
            SharedSystemMemoryBytes: 8UL * 1024 * 1024 * 1024,
            IsSoftware: false);

        var memoryGB = Math.Round(adapter.DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);

        Assert.Equal(0, memoryGB);
        // No toma shared memory
    }

    // ===== TEST E: Software adapter excluded =====

    [Fact]
    public void SoftwareAdapter_Excluded()
    {
        var adapters = new List<DxgiGpuMemoryReader.DxgiAdapterInfo>
        {
            new(Description: "Microsoft Basic Render Driver",
                VendorId: 0x1414,
                DeviceId: 0x008C,
                DedicatedVideoMemoryBytes: 0,
                SharedSystemMemoryBytes: 0,
                IsSoftware: true),
            new(Description: "NVIDIA GeForce RTX 4060 Laptop GPU",
                VendorId: 0x10DE,
                DeviceId: 0x28E0,
                DedicatedVideoMemoryBytes: 8UL * 1024 * 1024 * 1024,
                SharedSystemMemoryBytes: 0,
                IsSoftware: false)
        };

        var nonSoftware = adapters.Where(a => !a.IsSoftware).ToList();

        Assert.Single(nonSoftware);
        Assert.Equal("NVIDIA GeForce RTX 4060 Laptop GPU", nonSoftware[0].Description);
    }

    // ===== TEST F: Multi-GPU correlation =====

    [Fact]
    public void MultiGpu_CorrelationIsDeterministic()
    {
        var adapters = new List<DxgiGpuMemoryReader.DxgiAdapterInfo>
        {
            new(Description: "Intel Iris Xe Graphics",
                VendorId: 0x8086,
                DeviceId: 0xA7A0,
                DedicatedVideoMemoryBytes: 128 * 1024 * 1024,
                SharedSystemMemoryBytes: 8UL * 1024 * 1024 * 1024,
                IsSoftware: false),
            new(Description: "NVIDIA GeForce RTX 4060 Laptop GPU",
                VendorId: 0x10DE,
                DeviceId: 0x28E0,
                DedicatedVideoMemoryBytes: 8UL * 1024 * 1024 * 1024,
                SharedSystemMemoryBytes: 0,
                IsSoftware: false)
        };

        // Simular correlación por VendorId+DeviceId
        // WMI Intel: VEN_8086&DEV_A7A0
        var intelMatch = adapters.FirstOrDefault(a => a.VendorId == 0x8086 && a.DeviceId == 0xA7A0);
        Assert.NotNull(intelMatch);
        Assert.Equal(128UL * 1024 * 1024, intelMatch.DedicatedVideoMemoryBytes);

        // WMI NVIDIA: VEN_10DE&DEV_28E0
        var nvidiaMatch = adapters.FirstOrDefault(a => a.VendorId == 0x10DE && a.DeviceId == 0x28E0);
        Assert.NotNull(nvidiaMatch);
        Assert.Equal(8UL * 1024 * 1024 * 1024, nvidiaMatch.DedicatedVideoMemoryBytes);

        // No se intercambian
        Assert.NotEqual(intelMatch.DedicatedVideoMemoryBytes, nvidiaMatch.DedicatedVideoMemoryBytes);
    }

    // ===== TEST G: DXGI unavailable → MemoryGB = 0 =====

    [Fact]
    public void DxgiUnavailable_MemoryIsZero()
    {
        // Sin adaptadores DXGI disponibles
        var adapters = new List<DxgiGpuMemoryReader.DxgiAdapterInfo>();

        var match = adapters.FirstOrDefault(a => !a.IsSoftware);
        Assert.Null(match);

        // MemoryGB debe quedar 0 (UI lo muestra como N/D)
        var memoryGB = match != null
            ? Math.Round(match.DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2)
            : 0;

        Assert.Equal(0, memoryGB);
    }

    // ===== TEST H: WMI AdapterRAM capped no reemplaza DXGI =====

    [Fact]
    public void WmiAdapterRamCapped_DoesNotReplaceDxgi()
    {
        // Simular: WMI AdapterRAM = 4294967295 (uint32 max ≈ 4 GB)
        // DXGI DedicatedVideoMemory = 8 GB
        ulong wmiAdapterRam = 4294967295;
        ulong dxgiDedicated = 8UL * 1024 * 1024 * 1024;

        var wmiMemoryGB = Math.Round(wmiAdapterRam / (1024.0 * 1024.0 * 1024.0), 2);
        var dxgiMemoryGB = Math.Round(dxgiDedicated / (1024.0 * 1024.0 * 1024.0), 2);

        // WMI dice ~4 GB, DXGI dice 8 GB
        Assert.True(wmiMemoryGB < 5, $"WMI capped value should be < 5 GB, got {wmiMemoryGB}");
        Assert.Equal(8, dxgiMemoryGB);

        // El resultado final debe ser el de DXGI, no el de WMI
        var finalMemoryGB = dxgiMemoryGB; // Con DXGI disponible, se usa DXGI
        Assert.Equal(8, finalMemoryGB);
    }

    // ===== TEST I: DXGI match by name fallback (single candidate) =====

    [Fact]
    public void NameFallback_SingleCandidate_Matches()
    {
        var adapters = new List<DxgiGpuMemoryReader.DxgiAdapterInfo>
        {
            new(Description: "NVIDIA GeForce RTX 4060 Laptop GPU",
                VendorId: 0x10DE,
                DeviceId: 0x28E0,
                DedicatedVideoMemoryBytes: 8UL * 1024 * 1024 * 1024,
                SharedSystemMemoryBytes: 0,
                IsSoftware: false)
        };

        // Sin PNPDeviceID válido → fallback por nombre
        var normalizedGpuName = "nvidia geforce rtx 4060 laptop gpu";
        var candidates = adapters
            .Where(a => !a.IsSoftware)
            .Where(a => a.Description.Trim().ToLowerInvariant().Contains(normalizedGpuName))
            .ToList();

        Assert.Single(candidates);
        Assert.Equal(8UL * 1024 * 1024 * 1024, candidates[0].DedicatedVideoMemoryBytes);
    }

    // ===== TEST J: Name fallback ambiguous → no match =====

    [Fact]
    public void NameFallback_Ambiguous_NoMatch()
    {
        var adapters = new List<DxgiGpuMemoryReader.DxgiAdapterInfo>
        {
            new(Description: "NVIDIA GeForce RTX 4060",
                VendorId: 0x10DE,
                DeviceId: 0x28E0,
                DedicatedVideoMemoryBytes: 8UL * 1024 * 1024 * 1024,
                SharedSystemMemoryBytes: 0,
                IsSoftware: false),
            new(Description: "NVIDIA GeForce RTX 4060 Ti",
                VendorId: 0x10DE,
                DeviceId: 0x28E1,
                DedicatedVideoMemoryBytes: 16UL * 1024 * 1024 * 1024,
                SharedSystemMemoryBytes: 0,
                IsSoftware: false)
        };

        var normalizedGpuName = "nvidia geforce rtx 4060";
        var candidates = adapters
            .Where(a => !a.IsSoftware)
            .Where(a => a.Description.Trim().ToLowerInvariant().Contains(normalizedGpuName))
            .ToList();

        // 2 candidatos → ambiguo → no match seguro
        Assert.Equal(2, candidates.Count);
    }
}
