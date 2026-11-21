namespace NintendoDiscordStatus.Core.Abstractions
{
    public interface IProcessRunner
    {
        int GetCurrentProcessId();
        int RunAndWait(string FilePath, string Arguments);
        void StartHiddenShell(string FilePath, string Arguments);
        void OpenUrl(string Url);
        void ExitApplication(int ExitCode);
    }
}
