using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Forge.Terminal.Native;

/// <summary>
/// Põe o próprio app num Job Object com "encerrar ao fechar". Tudo o que ele inicia (shells, servidores npm,
/// wsl.exe, o conhost do ConPTY) herda o job: se o Forge fechar ou cair, o Windows encerra os filhos e nenhum
/// servidor fica órfão em segundo plano.
/// </summary>
public static partial class JobObject
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    // Fica aberto até o processo terminar: o fechamento do último handle é o que encerra os filhos.
    private static nint _job;

    /// <summary>Liga o encerramento dos filhos. Devolve false se o Windows recusar (o app segue, sem a garantia).</summary>
    public static bool KillChildrenOnExit()
    {
        if (_job != 0)
            return true;

        var job = CreateJobObjectW(0, null);
        if (job == 0)
            return false;

        var info = new JobObjectExtendedLimitInformationData();
        info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
        var ok = SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<JobObjectExtendedLimitInformationData>())
            && AssignCurrentProcess(job);
        if (!ok)
        {
            CloseHandle(job);
            return false;
        }
        _job = job;
        return true;
    }

    private static bool AssignCurrentProcess(nint job)
    {
        using var self = Process.GetCurrentProcess();
        return AssignProcessToJobObject(job, self.Handle);
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObjectW(nint attributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(nint job, int infoClass, ref JobObjectExtendedLimitInformationData info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(nint job, nint process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    // Estruturas do Win32 (JOBOBJECT_EXTENDED_LIMIT_INFORMATION). Só LimitFlags é preenchido; o resto fica zerado.
#pragma warning disable CS0649, CS0169
    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformationData
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
#pragma warning restore CS0649, CS0169
}
