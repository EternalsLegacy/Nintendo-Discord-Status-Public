using System;
using System.Collections.Generic;
using NintendoDiscordStatus.Core.Abstractions;

namespace NintendoDiscordStatus.Tests.Fakes;

public sealed class FakeProcessRunner : IProcessRunner
{
    public int FakeProcessId { get; set; } = 4242;
    public int RunAndWaitExitCode { get; set; } = 0;
    public Action<string, string>? OnRunAndWait { get; set; }

    public List<(string FilePath, string Arguments)> RunAndWaitCalls { get; } =
        new List<(string FilePath, string Arguments)>();

    public List<(string FilePath, string Arguments)> StartHiddenShellCalls { get; } =
        new List<(string FilePath, string Arguments)>();

    public List<string> OpenedUrls { get; } = new List<string>();
    public List<int> ExitCodes { get; } = new List<int>();

    public int GetCurrentProcessId() => FakeProcessId;

    public int RunAndWait(string FilePath, string Arguments)
    {
        RunAndWaitCalls.Add((FilePath, Arguments));
        OnRunAndWait?.Invoke(FilePath, Arguments);
        return RunAndWaitExitCode;
    }

    public void StartHiddenShell(string FilePath, string Arguments)
    {
        StartHiddenShellCalls.Add((FilePath, Arguments));
    }

    public void OpenUrl(string Url)
    {
        OpenedUrls.Add(Url);
    }

    public void ExitApplication(int ExitCode)
    {
        ExitCodes.Add(ExitCode);
    }
}
