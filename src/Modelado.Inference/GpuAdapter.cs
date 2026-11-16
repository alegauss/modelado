using System.Globalization;
using System.Runtime.InteropServices;

namespace Modelado.Inference;

/// <summary>
/// A GPU DirectML can run on, as the provider chip names it (MOD52), after PhotoBoost's
/// ProviderDetector. DirectML's device id indexes DXGI's adapters, so an adapter is read through
/// DXGI: its description, dedicated video memory and index. MOD147: whether it is integrated is
/// DXCore's answer (<c>IDXCoreAdapter</c>'s IsIntegrated, the adapter found by DXGI's LUID), since
/// dedicated memory does not tell: an Intel UHD 770 reports 2 GB. Read through a few vtable calls
/// rather than a binding package, since that is all it needs.
/// </summary>
/// <param name="Name">The adapter's description, e.g. "NVIDIA GeForce RTX 4060".</param>
/// <param name="VideoMemory">Its dedicated video memory, in bytes.</param>
/// <param name="Index">DXGI's index for it, the device id DirectML takes.</param>
/// <param name="Integrated">MOD147: true for a GPU that is part of the processor, false for a discrete one, null where DXCore cannot say.</param>
public sealed record GpuAdapter(string Name, long VideoMemory, int Index = 0, bool? Integrated = null)
{
    private const int EnumAdapters1Slot = 12; // IUnknown 3, IDXGIObject 4, IDXGIFactory 5
    private const int GetDesc1Slot = 10; // IUnknown 3, IDXGIObject 4, IDXGIAdapter 3
    private const int ReleaseSlot = 2;
    private const int GetAdapterByLuidSlot = 4; // IUnknown 3, then CreateAdapterList
    private const int GetPropertySlot = 6; // IUnknown 3, IsValid, IsAttributeSupported, IsPropertySupported
    private const uint IsIntegratedProperty = 12; // DXCoreAdapterProperty::IsIntegrated
    private const uint SoftwareFlag = 2; // DXGI_ADAPTER_FLAG_SOFTWARE
    private const int MaxAdapters = 16;
    private static readonly Guid FactoryId = new("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
    private static readonly Guid CoreFactoryId = new("78ee5945-c36e-4b13-a669-005dd11c0f06"); // IDXCoreAdapterFactory
    private static readonly Guid CoreAdapterId = new("f0db4c7f-fe5a-42a2-bd62-f2a6cf6fc83e"); // IDXCoreAdapter

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1(nint self, uint index, out nint adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1(nint self, out AdapterDesc1 desc);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Release(nint self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAdapterByLuid(nint self, in Luid luid, in Guid riid, out nint adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetProperty(nint self, uint property, nuint size, out byte value);

    /// <summary>The chip's words for it: "NVIDIA GeForce RTX 4060, 8 GB".</summary>
    public string Label => $"{Name}, {Memory(VideoMemory)}";

    /// <summary>The adapter DirectML's device <paramref name="deviceId"/> is, or null when there is none to name.</summary>
    /// <param name="deviceId">DirectML's device id, DXGI's adapter index.</param>
    /// <returns>Null off Windows, when DXGI cannot be asked, or for the Microsoft Basic Render Driver a VM has.</returns>
    public static GpuAdapter? ForDevice(int deviceId = 0) =>
        deviceId < 0 ? null : All().FirstOrDefault(adapter => adapter.Index == deviceId);

    /// <summary>MOD147: every hardware adapter DXGI lists, in its order; none off Windows or where DXGI cannot be asked.</summary>
    /// <returns>The adapters, software ones left out.</returns>
    public static IReadOnlyList<GpuAdapter> All()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            return Read();
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            return [];
        }
    }

    /// <summary>A byte count as the chip says it: whole gigabytes, or megabytes under one.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>"8 GB", "512 MB".</returns>
    public static string Memory(long bytes) => bytes >= 1L << 30
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(bytes / (double)(1L << 30)):0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes >> 20} MB");

    private static List<GpuAdapter> Read()
    {
        List<GpuAdapter> adapters = [];
        if (CreateDXGIFactory1(FactoryId, out nint factory) < 0)
        {
            return adapters;
        }

        nint core = CoreFactory();
        try
        {
            for (uint index = 0; index < MaxAdapters; index++)
            {
                if (Method<EnumAdapters1>(factory, EnumAdapters1Slot)(factory, index, out nint adapter) < 0)
                {
                    break; // DXGI_ERROR_NOT_FOUND: no more adapters
                }

                try
                {
                    if (Method<GetDesc1>(adapter, GetDesc1Slot)(adapter, out AdapterDesc1 desc) >= 0 && (desc.Flags & SoftwareFlag) == 0)
                    {
                        Luid luid = new() { Low = desc.LuidLow, High = desc.LuidHigh };
                        adapters.Add(new GpuAdapter(desc.Description.Trim(), (long)desc.DedicatedVideoMemory, (int)index, IsIntegrated(core, luid)));
                    }
                }
                finally
                {
                    Method<Release>(adapter, ReleaseSlot)(adapter);
                }
            }
        }
        finally
        {
            Method<Release>(factory, ReleaseSlot)(factory);
            if (core != 0)
            {
                Method<Release>(core, ReleaseSlot)(core);
            }
        }

        return adapters;
    }

    // DXCore is Windows 10 2004 and later; without it, whether an adapter is integrated is unknown.
    private static nint CoreFactory()
    {
        try
        {
            return DXCoreCreateAdapterFactory(CoreFactoryId, out nint factory) >= 0 ? factory : 0;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return 0;
        }
    }

    private static bool? IsIntegrated(nint core, Luid luid)
    {
        if (core == 0 || Method<GetAdapterByLuid>(core, GetAdapterByLuidSlot)(core, luid, CoreAdapterId, out nint adapter) < 0)
        {
            return null;
        }

        try
        {
            return Method<GetProperty>(adapter, GetPropertySlot)(adapter, IsIntegratedProperty, 1, out byte integrated) >= 0 ? integrated != 0 : null;
        }
        finally
        {
            Method<Release>(adapter, ReleaseSlot)(adapter);
        }
    }

    private static T Method<T>(nint instance, int slot)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * nint.Size));

    [DllImport("dxgi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CreateDXGIFactory1(in Guid riid, out nint factory);

    [DllImport("dxcore.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DXCoreCreateAdapterFactory(in Guid riid, out nint factory);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint Low;
        public int High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint LuidLow;
        public int LuidHigh;
        public uint Flags;
    }
}
