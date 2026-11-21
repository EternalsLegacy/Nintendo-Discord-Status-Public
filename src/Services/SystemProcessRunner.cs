using System;
using System.Diagnostics;
using NintendoDiscordStatus.Core.Abstractions;

namespace NintendoDiscordStatus.Services;

public class SystemProcessRunner : IProcessRunner
{
    public int GetCurrentProcessId()
    {
        return Environment.ProcessId;
    }

    public int RunAndWait(string FilePath, string Arguments)
    {
        ProcessStartInfo StartInfo = new ProcessStartInfo
        {
            FileName = FilePath,
            Arguments = Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using Process? RunningProcess = Process.Start(StartInfo);
        if (RunningProcess == null)
        {
            return -1;
        }

        RunningProcess.WaitForExit();
        return RunningProcess.ExitCode;
    }

    public void StartHiddenShell(string FilePath, string Arguments)
    {
        ProcessStartInfo StartInfo = new ProcessStartInfo
        {
            FileName = FilePath,
            Arguments = Arguments,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(StartInfo);
    }

    public void OpenUrl(string Url)
    {
        ProcessStartInfo StartInfo = new ProcessStartInfo
        {
            FileName = Url,
            UseShellExecute = true
        };

        Process.Start(StartInfo);
    }

    public void ExitApplication(int ExitCode)
    {
        Environment.Exit(ExitCode);
    }
}
