using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PCHealthDashboard.Helpers;

public static class MemoryHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    /// <summary>
    /// Fast unmanaged working set trim without triggering heavy CPU GC sweeps.
    /// </summary>
    public static void TrimWorkingSet()
    {
        try
        {
            IntPtr handle = Process.GetCurrentProcess().Handle;
            EmptyWorkingSet(handle);
            SetProcessWorkingSetSize(handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
    }

    /// <summary>
    /// Light GC cleanup and working set trim.
    /// </summary>
    public static void MinimizeMemory()
    {
        try
        {
            GC.Collect(1, GCCollectionMode.Optimized);
            TrimWorkingSet();
        }
        catch { }
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("ntdll.dll")]
    private static extern uint NtSetSystemInformation(int InfoClass, ref int Info, int Length);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    private const int SystemMemoryListInformation = 80;
    private const int MemoryEmptyWorkingSets = 2;
    private const int MemoryPurgeStandbyList = 4;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";

    /// <summary>
    /// Attempts to clear the Windows Standby List (System File Cache) and flush all processes' working sets.
    /// Requires Administrative Privileges.
    /// </summary>
    public static bool ClearSystemMemoryCache()
    {
        try
        {
            // First, minimize our own app's memory
            MinimizeMemory();

            IntPtr processHandle = Process.GetCurrentProcess().Handle;
            if (OpenProcessToken(processHandle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tokenHandle))
            {
                if (LookupPrivilegeValue(null, SE_PROF_SINGLE_PROCESS_NAME, out LUID luid))
                {
                    var tp = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Privileges = new LUID_AND_ATTRIBUTES[1]
                    };
                    tp.Privileges[0].Luid = luid;
                    tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

                    if (AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    {
                        // 1. Force all running processes (tabs, games, apps) to flush unused RAM
                        int commandEmpty = MemoryEmptyWorkingSets;
                        NtSetSystemInformation(SystemMemoryListInformation, ref commandEmpty, Marshal.SizeOf(commandEmpty));

                        // 2. Clear the Standby List to completely free the flushed RAM
                        int commandPurge = MemoryPurgeStandbyList;
                        uint result = NtSetSystemInformation(SystemMemoryListInformation, ref commandPurge, Marshal.SizeOf(commandPurge));
                        
                        return result == 0;
                    }
                }
            }
        }
        catch { }
        return false;
    }
}
