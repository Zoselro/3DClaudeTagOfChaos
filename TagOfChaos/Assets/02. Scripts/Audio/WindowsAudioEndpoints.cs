using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Windows 출력 장치 목록과 "이 게임만 다른 장치로" 설정(Request1009Plan.md §17). Unity에는 출력 장치를 고르는 기능이 없어
// Windows 기능을 직접 부른다. 플러그인 파일 없이 C#만으로 — Mono에서도 안전하게 COM 함수표를 직접 호출한다.
// - 장치 목록: Core Audio(IMMDeviceEnumerator, 공개 API).
// - 앱별 장치: Windows.Media.Internal.AudioPolicyConfig(Windows 설정 "앱 볼륨 및 장치 기본 설정"이 쓰는 내부 기능, 문서 없음).
//   PC 전체 기본 장치는 바꾸지 않는다. Windows 10 21H2 이후와 그 전은 인터페이스 번호가 달라 둘 다 시도한다.
// - 확인용: 장치별로 이 프로세스의 소리 세션이 있는지(IAudioSessionManager2).
// 실패하면 예외 대신 false/빈 목록을 돌려준다(호출부가 "시스템 기본"으로 남는다).
public static class WindowsAudioEndpoints
{
    public readonly struct Device
    {
        public readonly string Id;
        public readonly string Name;
        public Device(string id, string name) { Id = id; Name = name; }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const int ERender = 0;
    private const int DeviceStateActive = 1;
    private const int CLSCTX_ALL = 23;
    private const int RoleConsole = 0, RoleMultimedia = 1;
    private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    private static Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static Guid IID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static Guid IID_IAudioSessionControl2 = new Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
    private static readonly Guid[] PolicyConfigFactoryIids =
    {
        new Guid("ab3d4648-e242-459f-b02f-541c70306324"), // Windows 10 21H2 이후·Windows 11
        new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258"), // Windows 10 1803 ~ 21H1
    };
    private const string PolicyConfigClass = "Windows.Media.Internal.AudioPolicyConfig";
    private const string MmDevApiToken = @"\\?\SWD#MMDEVAPI#";
    private const string RenderInterface = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid fmtid; public int pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr pointer; }

    private static PropertyKey FriendlyName = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };

    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int ctx, ref Guid iid, out IntPtr obj);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, int coinit);
    [DllImport("ole32.dll")] private static extern void CoTaskMemFree(IntPtr p);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pv);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string s, int length, out IntPtr hstring);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr hstring);
    [DllImport("combase.dll")] private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out int length);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReleaseFn(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterfaceFn(IntPtr self, ref Guid iid, out IntPtr obj);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumEndpointsFn(IntPtr self, int flow, int mask, out IntPtr collection);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetDefaultEndpointFn(IntPtr self, int flow, int role, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int OutPtrFn(IntPtr self, out IntPtr obj);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetCountFn(IntPtr self, out int count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ItemFn(IntPtr self, int index, out IntPtr item);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ActivateFn(IntPtr self, ref Guid iid, int ctx, IntPtr parms, out IntPtr obj);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int OpenPropertyStoreFn(IntPtr self, int access, out IntPtr store);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetIdFn(IntPtr self, out IntPtr id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetValueFn(IntPtr self, ref PropertyKey key, out PropVariant value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetStateFn(IntPtr self, out int state);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetProcessIdFn(IntPtr self, out uint pid);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetPersistedFn(IntPtr self, uint pid, int flow, int role, IntPtr deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPersistedFn(IntPtr self, uint pid, int flow, int role, out IntPtr deviceId);

    // IAudioPolicyConfigFactory 함수표 위치: IUnknown 3 + IInspectable 3 + 앞선 함수 19개.
    private const int SetPersistedSlot = 25, GetPersistedSlot = 26;

    private static T Method<T>(IntPtr obj, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size));

    private static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero) Method<ReleaseFn>(obj, 2)(obj);
    }

    public static bool IsSupported => true;

    private static IntPtr CreateEnumerator()
    {
        int hr = CoCreateInstance(ref CLSID_MMDeviceEnumerator, IntPtr.Zero, CLSCTX_ALL, ref IID_IMMDeviceEnumerator, out IntPtr e);
        if (hr < 0)
        {
            int init = CoInitializeEx(IntPtr.Zero, 0); // 이 스레드에 COM이 아직 없을 때만
            if (init >= 0 || init == RPC_E_CHANGED_MODE)
                hr = CoCreateInstance(ref CLSID_MMDeviceEnumerator, IntPtr.Zero, CLSCTX_ALL, ref IID_IMMDeviceEnumerator, out e);
        }
        return hr >= 0 ? e : IntPtr.Zero;
    }

    private static string DeviceId(IntPtr device)
    {
        if (Method<GetIdFn>(device, 5)(device, out IntPtr p) < 0 || p == IntPtr.Zero) return null;
        string id = Marshal.PtrToStringUni(p);
        CoTaskMemFree(p);
        return id;
    }

    private static string DeviceName(IntPtr device)
    {
        if (Method<OpenPropertyStoreFn>(device, 4)(device, 0, out IntPtr store) < 0) return null;
        try
        {
            if (Method<GetValueFn>(store, 5)(store, ref FriendlyName, out PropVariant pv) < 0) return null;
            string name = pv.vt == 31 && pv.pointer != IntPtr.Zero ? Marshal.PtrToStringUni(pv.pointer) : null; // VT_LPWSTR
            PropVariantClear(ref pv);
            return name;
        }
        finally { Release(store); }
    }

    // 켜져 있는 출력 장치(재생 장치) 목록.
    public static List<Device> ListOutputs()
    {
        var list = new List<Device>();
        IntPtr enumerator = CreateEnumerator();
        if (enumerator == IntPtr.Zero) return list;
        IntPtr collection = IntPtr.Zero;
        try
        {
            if (Method<EnumEndpointsFn>(enumerator, 3)(enumerator, ERender, DeviceStateActive, out collection) < 0) return list;
            Method<GetCountFn>(collection, 3)(collection, out int count);
            for (int i = 0; i < count; i++)
            {
                if (Method<ItemFn>(collection, 4)(collection, i, out IntPtr device) < 0) continue;
                try
                {
                    string id = DeviceId(device);
                    if (id != null) list.Add(new Device(id, DeviceName(device) ?? id));
                }
                finally { Release(device); }
            }
        }
        catch (Exception) { list.Clear(); }
        finally { Release(collection); Release(enumerator); }
        return list;
    }

    // Windows 전체 기본 출력 장치 ID.
    public static string SystemDefaultId()
    {
        IntPtr enumerator = CreateEnumerator();
        if (enumerator == IntPtr.Zero) return null;
        try
        {
            if (Method<GetDefaultEndpointFn>(enumerator, 4)(enumerator, ERender, RoleMultimedia, out IntPtr device) < 0) return null;
            try { return DeviceId(device); } finally { Release(device); }
        }
        catch (Exception) { return null; }
        finally { Release(enumerator); }
    }

    private static IntPtr PolicyFactory()
    {
        if (WindowsCreateString(PolicyConfigClass, PolicyConfigClass.Length, out IntPtr cls) < 0) return IntPtr.Zero;
        try
        {
            foreach (Guid g in PolicyConfigFactoryIids)
            {
                Guid iid = g;
                if (RoGetActivationFactory(cls, ref iid, out IntPtr factory) >= 0 && factory != IntPtr.Zero) return factory;
            }
            return IntPtr.Zero;
        }
        catch (Exception) { return IntPtr.Zero; }
        finally { WindowsDeleteString(cls); }
    }

    public static string PackDeviceId(string deviceId) => MmDevApiToken + deviceId + RenderInterface;

    public static string UnpackDeviceId(string packed)
    {
        if (string.IsNullOrEmpty(packed)) return null;
        string s = packed;
        if (s.StartsWith(MmDevApiToken, StringComparison.OrdinalIgnoreCase)) s = s.Substring(MmDevApiToken.Length);
        int hash = s.IndexOf("#{", StringComparison.Ordinal);
        return hash >= 0 ? s.Substring(0, hash) : s;
    }

    // 이 프로세스의 출력 장치를 정한다(null·빈 값 = 시스템 기본으로 되돌림). Unity가 새 장치를 열려면 이어서 AudioSettings.Reset이 필요하다.
    public static bool SetAppOutput(string deviceId)
    {
        IntPtr factory = PolicyFactory();
        if (factory == IntPtr.Zero) return false;
        IntPtr h = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(deviceId))
            {
                string packed = PackDeviceId(deviceId);
                if (WindowsCreateString(packed, packed.Length, out h) < 0) return false;
            }
            uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            var set = Method<SetPersistedFn>(factory, SetPersistedSlot);
            return set(factory, pid, ERender, RoleMultimedia, h) >= 0 && set(factory, pid, ERender, RoleConsole, h) >= 0;
        }
        catch (Exception) { return false; }
        finally
        {
            if (h != IntPtr.Zero) WindowsDeleteString(h);
            Release(factory);
        }
    }

    // 이 프로세스에 정해진 출력 장치(없으면 null = 시스템 기본).
    public static string GetAppOutput()
    {
        IntPtr factory = PolicyFactory();
        if (factory == IntPtr.Zero) return null;
        try
        {
            uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            if (Method<GetPersistedFn>(factory, GetPersistedSlot)(factory, pid, ERender, RoleMultimedia, out IntPtr h) < 0 || h == IntPtr.Zero) return null;
            IntPtr raw = WindowsGetStringRawBuffer(h, out int len);
            string packed = Marshal.PtrToStringUni(raw, len);
            WindowsDeleteString(h);
            return UnpackDeviceId(packed);
        }
        catch (Exception) { return null; }
        finally { Release(factory); }
    }

    // 확인용: 이 프로세스가 그 장치에 소리 세션을 갖고 있는지(Unity가 실제로 그 장치로 소리를 내는지).
    // activeOnly = 지금 소리를 내는 중(상태 1)인 세션만 — 장치를 바꾸면 예전 장치에는 쉬는 세션(상태 0)이 남는다.
    public static bool ProcessHasSessionOn(string deviceId, bool activeOnly = false)
    {
        IntPtr enumerator = CreateEnumerator();
        if (enumerator == IntPtr.Zero) return false;
        IntPtr collection = IntPtr.Zero;
        uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        try
        {
            if (Method<EnumEndpointsFn>(enumerator, 3)(enumerator, ERender, DeviceStateActive, out collection) < 0) return false;
            Method<GetCountFn>(collection, 3)(collection, out int count);
            for (int i = 0; i < count; i++)
            {
                if (Method<ItemFn>(collection, 4)(collection, i, out IntPtr device) < 0) continue;
                try
                {
                    if (DeviceId(device) != deviceId) continue;
                    return HasSession(device, me, activeOnly);
                }
                finally { Release(device); }
            }
            return false;
        }
        catch (Exception) { return false; }
        finally { Release(collection); Release(enumerator); }
    }

    // 그 장치에 이 프로세스의 세션이 있고 끝나지 않았는지(상태 0 = 쉬는 중, 1 = 재생 중, 2 = 끝남).
    private static bool HasSession(IntPtr device, uint pid, bool activeOnly)
    {
        if (Method<ActivateFn>(device, 3)(device, ref IID_IAudioSessionManager2, CLSCTX_ALL, IntPtr.Zero, out IntPtr manager) < 0) return false;
        IntPtr sessions = IntPtr.Zero;
        try
        {
            if (Method<OutPtrFn>(manager, 5)(manager, out sessions) < 0) return false; // GetSessionEnumerator
            Method<GetCountFn>(sessions, 3)(sessions, out int count);
            for (int i = 0; i < count; i++)
            {
                if (Method<ItemFn>(sessions, 4)(sessions, i, out IntPtr control) < 0) continue;
                IntPtr control2 = IntPtr.Zero;
                try
                {
                    if (Method<QueryInterfaceFn>(control, 0)(control, ref IID_IAudioSessionControl2, out control2) < 0) continue;
                    Method<GetProcessIdFn>(control2, 14)(control2, out uint owner);
                    Method<GetStateFn>(control2, 3)(control2, out int state);
                    if (owner == pid && (activeOnly ? state == 1 : state != 2)) return true;
                }
                finally { Release(control2); Release(control); }
            }
            return false;
        }
        finally { Release(sessions); Release(manager); }
    }
#else
    public static bool IsSupported => false;
    public static List<Device> ListOutputs() => new List<Device>();
    public static string SystemDefaultId() => null;
    public static bool SetAppOutput(string deviceId) => false;
    public static string GetAppOutput() => null;
    public static bool ProcessHasSessionOn(string deviceId, bool activeOnly = false) => false;
    public static string PackDeviceId(string deviceId) => deviceId;
    public static string UnpackDeviceId(string packed) => packed;
#endif
}
