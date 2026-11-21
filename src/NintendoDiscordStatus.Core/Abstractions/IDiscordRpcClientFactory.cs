namespace NintendoDiscordStatus.Core.Abstractions
{
    public interface IDiscordRpcClientFactory
    {
        IDiscordRpcClient CreateClient(string ApplicationId);
    }
}
