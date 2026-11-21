using System;
using NintendoDiscordStatus.Core.Abstractions;

namespace NintendoDiscordStatus.Tests.Fakes;

public sealed class FakeTimeProvider : ITimeProvider
{
    public FakeTimeProvider(DateTime? InitialUtc = null)
    {
        UtcNow = InitialUtc ?? new DateTime(2026, 11, 11, 18, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan Delta)
    {
        UtcNow = UtcNow.Add(Delta);
    }
}
