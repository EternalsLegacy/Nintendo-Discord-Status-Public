using System;

namespace NintendoDiscordStatus.Core.Abstractions
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
