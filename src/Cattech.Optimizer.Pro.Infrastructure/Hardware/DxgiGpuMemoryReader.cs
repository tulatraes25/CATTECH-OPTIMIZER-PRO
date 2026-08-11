using System.Runtime.InteropServices;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Lee la memoria dedicada de GPUs mediante DXGI 1.1.
/// Reemplaza Win32_VideoController.AdapterRAM que es uint32 y se satura en ~4 GB.
/// </summary>
internal static class DxgiGpuMemoryReader
{
    /// <summary>
    /// Resultado de un adaptador DXGI enumerado.
    /// </summary>
    internal sealed record DxgiAdapterInfo(
        string Description,
        uint VendorId,
        uint DeviceId,
        ulong DedicatedVideoMemoryBytes,
        ulong SharedSystemMemoryBytes,
        bool IsSoftware);

    /// <summary>
    /// Enumera adaptadores DXGI físicos (excluye software) y retorna su información.
    /// </summary>
    internal static List<DxgiAdapterInfo> EnumerateAdapters()
    {
        var result = new List<DxgiAdapterInfo>();

        var hr = CreateDXGIFactory1(typeof(IDXGIFactory1).GUID, out var factoryObj);
        if (hr != 0 || factoryObj is not IDXGIFactory1 factory)
        {
            Marshal.ReleaseComObject(factoryObj!);
            return result;
        }

        try
        {
            uint index = 0;
            while (factory.EnumAdapters1(index, out var adapter) == 0)
            {
                try
                {
                    var desc = adapter.GetDesc1();

                    bool isSoftware = (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0;
                    string description = desc.Description ?? string.Empty;

                    result.Add(new DxgiAdapterInfo(
                        Description: description,
                        VendorId: desc.VendorId,
                        DeviceId: desc.DeviceId,
                        DedicatedVideoMemoryBytes: desc.DedicatedVideoMemory,
                        SharedSystemMemoryBytes: desc.SharedSystemMemory,
                        IsSoftware: isSoftware));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }

                index++;
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }

        return result;
    }

    // ===== DXGI COM Interop (mínimo necesario) =====

    private const uint DXGI_ADAPTER_FLAG_SOFTWARE = 0x2;

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(
        [In] Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object? ppFactory);

    [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        // IDXGIFactory methods (vtable slot 0-6) — not used directly, only IDXGIFactory1.EnumAdapters1
        void _VtblSlot0(); // EnumAdapters
        void _VtblSlot1(); // MakeWindowAssociation
        void _VtblSlot2(); // GetWindowAssociation
        void _VtblSlot3(); // CreateSwapChain
        void _VtblSlot4(); // CreateSoftwareAdapter

        // IDXGIFactory1
        [PreserveSig]
        int EnumAdapters1(uint adapterIndex, out IDXGIAdapter1 ppAdapter);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsCurrent();
    }

    [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        // IDXGIObject
        void _VtblSlot0(); // SetPrivateData
        void _VtblSlot1(); // SetPrivateDataInterface
        void _VtblSlot2(); // GetPrivateData
        void _VtblSlot3(); // GetParent

        // IDXGIAdapter
        void _VtblSlot4(); // EnumOutputs
        void _VtblSlot5(); // GetDesc
        void _VtblSlot6(); // CheckInterfaceSupport

        // IDXGIAdapter1
        DXGI_ADAPTER_DESC1 GetDesc1();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public ulong DedicatedVideoMemory;
        public ulong DedicatedSystemMemory;
        public ulong SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }
}
