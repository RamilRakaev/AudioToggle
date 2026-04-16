using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            // Если нужно просто посмотреть порядок устройств (необязательно)
            if (args.Length > 0 && args.Any(a => a.Equals("--list", StringComparison.OrdinalIgnoreCase)))
            {
                ListDevices(enumerator);
                return 0;
            }

            // Берём только активные устройства вывода
            var devices = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                // Стабильный "порядок", чтобы при каждом запуске он был одинаковый
                .OrderBy(d => d.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.ID, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (devices.Count == 0)
            {
                Console.Error.WriteLine("Не найдено активных устройств вывода (Render).");
                return 2;
            }

            if (devices.Count == 1)
            {
                Console.WriteLine($"Активно только одно устройство: {devices[0].FriendlyName}. Переключать не на что.");
                return 0;
            }

            var current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            int currentIndex = devices.FindIndex(d => d.ID.Equals(current.ID, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                // Если текущее дефолтное почему-то не попало в Active-список — выберем первое
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + 1) % devices.Count;
            var next = devices[nextIndex];

            SetDefaultAudioEndpoint(next.ID);

            Console.WriteLine($"Было : {current.FriendlyName}");
            Console.WriteLine($"Стало: {next.FriendlyName}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void ListDevices(MMDeviceEnumerator enumerator)
    {
        var devices = enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .OrderBy(d => d.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => d.ID, StringComparer.OrdinalIgnoreCase)
            .ToList();

        MMDevice? current = null;
        try { current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); } catch { }

        Console.WriteLine("Активные устройства вывода (Render) в том порядке, в котором будет переключение:");
        Console.WriteLine();

        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            var mark = (current != null && d.ID.Equals(current.ID, StringComparison.OrdinalIgnoreCase)) ? "*" : " ";
            Console.WriteLine($"{mark} {i}: {d.FriendlyName}");
        }
    }

    private static void SetDefaultAudioEndpoint(string deviceId)
    {
        var policyConfig = (IPolicyConfig)new PolicyConfigClient();
        Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole));
        Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
        Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications));
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient { }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, out IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole eRole);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
        [FieldOffset(8)] public int intValue;
        [FieldOffset(8)] public uint uintValue;
        [FieldOffset(8)] public long longValue;
        [FieldOffset(8)] public ulong ulongValue;
    }
}
