using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="EventRejection"/>. Covers API Reference §8.1–5.
/// </summary>
public sealed class EventRejectionTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Mirrors the §7 "EventRejection (rate-limit case)" example.
    /// </summary>
    private static EventRejection SampleRateLimit()
    {
        return new EventRejection(
            Event: "slot-spin",
            Reason: RejectionReason.RateLimitExceeded,
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            RetryAfterMs: 250);
    }

    [Fact]
    public void RoundTrip_RateLimitWithRetryAfter_PreservesAllValues()
    {
        // Arrange.
        var original = SampleRateLimit();

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<EventRejection>(json, _options);

        // Assert.
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void RoundTrip_DuplicateInFlightWithNullRetryAfter_PreservesNull()
    {
        // Arrange — for DuplicateInFlight the bridge does NOT set
        // RetryAfterMs (retry won't help; the original is still in-flight).
        var original = new EventRejection(
            Event: "slot-spin",
            Reason: RejectionReason.DuplicateInFlight,
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            RetryAfterMs: null);

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<EventRejection>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.RetryAfterMs);
        Assert.Equal(RejectionReason.DuplicateInFlight, roundTripped.Reason);
    }

    [Fact]
    public void Deserialize_JsonWithUnknownProperty_DoesNotThrow()
    {
        // Arrange — §8.4: unknown property tolerance.
        const string json = """
            {
              "event": "slot-spin",
              "reason": "rate_limit_exceeded",
              "traceId": "9f86d081884c7d659a2feaa0c55ad015",
              "retryAfterMs": 250,
              "xyz": 1
            }
            """;

        // Act.
        var result = JsonSerializer.Deserialize<EventRejection>(json, _options);

        // Assert.
        Assert.NotNull(result);
        Assert.Equal("slot-spin", result.Event);
        Assert.Equal(250, result.RetryAfterMs);
    }

    [Fact]
    public void Serialize_RateLimitSample_ProducesExpectedJson()
    {
        // Arrange — §8.5: golden wire format for the §7 rate-limit example.
        var sample = SampleRateLimit();
        const string expected =
            "{" +
            "\"event\":\"slot-spin\"," +
            "\"reason\":\"rate_limit_exceeded\"," +
            "\"traceId\":\"9f86d081884c7d659a2feaa0c55ad015\"," +
            "\"retryAfterMs\":250" +
            "}";

        // Act.
        var json = JsonSerializer.Serialize(sample, _options);

        // Assert.
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Equality_TwoRecordsWithSameFieldValues_AreEqual()
    {
        // Arrange — §8.3. EventRejection has no ReadOnlyMemory or collection
        // fields, so record-generated value equality works correctly here.
        var a = SampleRateLimit();
        var b = SampleRateLimit();

        // Act + Assert.
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferingReason_AreNotEqual()
    {
        // Arrange.
        var a = SampleRateLimit();
        var b = a with { Reason = RejectionReason.DuplicateInFlight };

        // Act + Assert.
        Assert.NotEqual(a, b);
    }
}
