using System;
using NintendoDiscordStatus.Core.Abstractions;

namespace NintendoDiscordStatus.Core.Services;

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
