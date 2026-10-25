using System.Globalization;
using System.Runtime.InteropServices;

namespace Modelado.Inference;

/// <summary>
/// The GPU DirectML runs on, as the provider chip names it (MOD52), after PhotoBoost's
/// ProviderDetector. DirectML's device id indexes DXGI's adapters, and Modelado always asks for
/// device 0, so that is the adapter read: its description and dedicated video memory. Read
/// through two DXGI vtable calls rather than a binding package, since that is all it needs.
/// </summary>
/// <param name="Name">The adapter's description, e.g. "NVIDIA GeForce RTX 4060".</param>
/// <param name="VideoMemory">Its dedicated video memory, in bytes.</param>
public sealed record GpuAdapter(string Name, long VideoMemory)
{
    private const int EnumAdapters1Slot = 12; // IUnknown 3, IDXGIObject 4, IDXGIFactory 5
    private const int GetDesc1Slot = 10; // IUnknown 3, IDXGIObject 4, IDXGIAdapter 3
    private const int ReleaseSlot = 2;
    private const uint SoftwareFlag = 2; // DXGI_ADAPTER_FLAG_SOFTWARE
    private static readonly Guid FactoryId = new("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1(nint self, uint index, out nint adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1(nint self, out AdapterDesc1 desc);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Release(nint self);

    /// <summary>The chip's words for it: "NVIDIA GeForce RTX 4060, 8 GB".</summary>
    public string Label => $"{Name}, {Memory(VideoMemory)}";

    /// <summary>The adapter DirectML's device <paramref name="deviceId"/> is, or null when there is none to name.</summary>
    /// <param name="deviceId">DirectML's device id, DXGI's adapter index.</param>
    /// <returns>Null off Windows, when DXGI cannot be asked, or for the Microsoft Basic Render Driver a VM has.</returns>
    public static GpuAdapter? ForDevice(int deviceId = 0)
    {
        if (!OperatingSystem.IsWindows() || deviceId < 0)
        {
            return null;
        }

        try
        {
            return Read((uint)deviceId);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            return null;
        }
    }

    /// <summary>A byte count as the chip says it: whole gigabytes, or megabytes under one.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>"8 GB", "512 MB".</returns>
    public static string Memory(long bytes) => bytes >= 1L << 30
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(bytes / (double)(1L << 30)):0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes >> 20} MB");

    private static GpuAdapter? Read(uint index)
    {
        if (CreateDXGIFactory1(FactoryId, out nint factory) < 0)
        {
            return null;
        }

        try
        {
            if (Method<EnumAdapters1>(factory, EnumAdapters1Slot)(factory, index, out nint adapter) < 0)
            {
                return null;
            }

            try
            {
                if (Method<GetDesc1>(adapter, GetDesc1Slot)(adapter, out AdapterDesc1 desc) < 0 || (desc.Flags & SoftwareFlag) != 0)
                {
                    return null;
                }

                return new GpuAdapter(desc.Description.Trim(), (long)desc.DedicatedVideoMemory);
            }
            finally
            {
                Method<Release>(adapter, ReleaseSlot)(adapter);
            }
        }
        finally
        {
            Method<Release>(factory, ReleaseSlot)(factory);
        }
    }

    private static T Method<T>(nint instance, int slot)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * nint.Size));

    [DllImport("dxgi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CreateDXGIFactory1(in Guid riid, out nint factory);

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
