using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.Process;

/// <summary>Kills tracked child processes when the desktop process exits.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsJobObjectProcessTracker
{
    private static readonly IntPtr JobHandle = CreateConfiguredJob();

    public static void AddProcess(System.Diagnostics.Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (JobHandle != IntPtr.Zero && !process.HasExited)
            AssignProcessToJobObject(JobHandle, process.Handle);
    }

    public static void AddProcessHandle(IntPtr processHandle)
    {
        if (JobHandle != IntPtr.Zero && processHandle != IntPtr.Zero)
            AssignProcessToJobObject(JobHandle, processHandle);
    }

    private static IntPtr CreateConfiguredJob()
    {
        IntPtr jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (jobHandle == IntPtr.Zero)
            return IntPtr.Zero;

        var info = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose }
        };
        int length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        IntPtr infoPointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPointer, false);
            if (!SetInformationJobObject(jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPointer, (uint)length))
            {
                CloseHandle(jobHandle);
                return IntPtr.Zero;
            }

            return jobHandle;
        }
        finally { Marshal.FreeHGlobal(infoPointer); }
    }

    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private enum JobObjectInfoType { ExtendedLimitInformation = 9 }
    [StructLayout(LayoutKind.Sequential)] private struct JobObjectBasicLimitInformation { public long PerProcessUserTimeLimit; public long PerJobUserTimeLimit; public uint LimitFlags; public nuint MinimumWorkingSetSize; public nuint MaximumWorkingSetSize; public uint ActiveProcessLimit; public long Affinity; public uint PriorityClass; public uint SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperationCount; public ulong WriteOperationCount; public ulong OtherOperationCount; public ulong ReadTransferCount; public ulong WriteTransferCount; public ulong OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)] private struct JobObjectExtendedLimitInformation { public JobObjectBasicLimitInformation BasicLimitInformation; public IoCounters IoInfo; public nuint ProcessMemoryLimit; public nuint JobMemoryLimit; public nuint PeakProcessMemoryUsed; public nuint PeakJobMemoryUsed; }
}
