using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="RejectionReason"/>. Verifies that the const
/// string values are exactly what the wire expects — these are part of the
/// public contract and changing them breaks the client UI.
/// </summary>
public sealed class RejectionReasonTests
{
    [Fact]
    public void DuplicateInFlight_HasCanonicalSnakeCaseValue()
    {
        Assert.Equal("duplicate_in_flight", RejectionReason.DuplicateInFlight);
    }

    [Fact]
    public void RateLimitExceeded_HasCanonicalSnakeCaseValue()
    {
        Assert.Equal("rate_limit_exceeded", RejectionReason.RateLimitExceeded);
    }

    [Fact]
    public void ServerDraining_HasCanonicalSnakeCaseValue()
    {
        // Reserved for v1.x — verify the value is locked in now.
        Assert.Equal("server_draining", RejectionReason.ServerDraining);
    }

    [Fact]
    public void UnknownEvent_HasCanonicalSnakeCaseValue()
    {
        // Reserved for v1.x — verify the value is locked in now.
        Assert.Equal("unknown_event", RejectionReason.UnknownEvent);
    }
}
