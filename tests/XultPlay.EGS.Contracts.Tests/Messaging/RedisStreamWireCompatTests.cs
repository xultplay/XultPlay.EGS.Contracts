using System;
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using XultPlay.RedisStream.Serialization;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Wire-compatibility tests between this library and
/// <c>XultPlay.RedisStream.Serialization.JsonMessageSerializer</c> — covering
/// API Reference §8 test #11 (the Q8 verification).
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that records defined in this library can round-trip
/// through <c>JsonMessageSerializer</c> with default options (the same
/// options the bridge uses to publish to Redis today), and that records
/// serialized by this library can be consumed by <c>JsonMessageSerializer</c>'s
/// deserializer. Both directions are exercised.
/// </para>
/// <para>
/// Wire-format note: <c>JsonMessageSerializer</c>'s default
/// <c>JsonSerializerOptions</c> include <c>DefaultIgnoreCondition =
/// WhenWritingNull</c>, which omits null properties from output. This library
/// emits null properties explicitly (<c>"ackId": null</c>). The two paths
/// therefore produce DIFFERENT bytes when a nullable field is null — but the
/// records they reconstruct from each other's output are semantically equal,
/// because System.Text.Json treats "missing property" and "property: null"
/// the same way when deserializing into a nullable type. The bridge today
/// publishes via RedisStream with default options, so the wire actually omits
/// null fields; the spec §7 examples that show <c>"ackId": null</c> are
/// rendered for documentation clarity.
/// </para>
/// <para>
/// What this DOES prove:
/// the per-field <c>[JsonConverter(typeof(ReadOnlyMemoryByteJsonConverter))]</c>
/// attribute is honored by both libraries (so the
/// <c>ReadOnlyMemory&lt;byte&gt;</c> Payload field encodes/decodes correctly via
/// our converter in both directions); the bytes our converter writes are
/// byte-identical to what the built-in .NET 8 path writes
/// (proven separately by
/// <c>ReadOnlyMemoryByteJsonConverterTests.Write_MatchesBuiltInReadOnlyMemoryByteFormat</c>);
/// and a record produced by one library deserializes to a structurally equal
/// record via the other.
/// </para>
/// </remarks>
public sealed class RedisStreamWireCompatTests
{
    // Cached per CA1869. These match the Contracts library's serialization
    // conventions: camelCase property names, per-field converter pulled in
    // via attribute on Payload (no options-level Converters registration
    // needed).
    private static readonly JsonSerializerOptions _contractsOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // RedisStream's default serializer — same options the bridge uses today.
    private static readonly JsonMessageSerializer _redisStreamSerializer = new();

    /// <summary>
    /// Builds a fully populated CommandMessage. The payload contains the
    /// problematic '+' base64 character that would be escaped to \u002B if
    /// either library accidentally routed the payload through the
    /// JavaScriptEncoder. If either library does, deserialization still
    /// works (escapes parse back to the same chars), but byte-level wire
    /// format would have diverged — the separate
    /// Write_MatchesBuiltInReadOnlyMemoryByteFormat test is the byte-level
    /// regression sentinel.
    /// </summary>
    private static CommandMessage BuildFullyPopulatedSample()
    {
        return new CommandMessage(
            Type: "slot-spin",
            SocketIoSid: "abc123def456ghi789jkl012",
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            AccessToken: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.token",
            ClientId: "web-1",
            GameType: "slot",
            ListenerHost: "bridge-prod-3",
            PayloadEncoding: "encrypted",
            Payload: Convert.FromBase64String("U2FsdGVkX1+vupppZksvRf5pq5g5XjFRIipRkwB0K1Y96Qsv2L3svw=="),
            AckId: "42",
            ReceivedAt: new DateTimeOffset(2026, 5, 22, 14, 30, 45, 123, TimeSpan.Zero));
    }

    private static void AssertCommandMessagesAreStructurallyEqual(
        CommandMessage expected,
        CommandMessage actual)
    {
        // ReadOnlyMemory<byte>.Equals is reference-based, so we compare
        // field-by-field with byte-content equality on Payload.
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.SocketIoSid, actual.SocketIoSid);
        Assert.Equal(expected.TraceId, actual.TraceId);
        Assert.Equal(expected.AccessToken, actual.AccessToken);
        Assert.Equal(expected.ClientId, actual.ClientId);
        Assert.Equal(expected.GameType, actual.GameType);
        Assert.Equal(expected.ListenerHost, actual.ListenerHost);
        Assert.Equal(expected.PayloadEncoding, actual.PayloadEncoding);
        Assert.True(expected.Payload.Span.SequenceEqual(actual.Payload.Span));
        Assert.Equal(expected.AckId, actual.AckId);
        Assert.Equal(expected.ReceivedAt, actual.ReceivedAt);
    }

    [Fact]
    public void RedisStreamSerializes_ContractsDeserializes_PreservesAllFields()
    {
        // Arrange — scenario 1: RedisStream's JsonMessageSerializer produces
        // the wire bytes (as the bridge does today), Contracts' plain
        // JsonSerializer.Deserialize<CommandMessage> reads them back using
        // only the per-field [JsonConverter] attribute on Payload.
        var original = BuildFullyPopulatedSample();

        // Act.
        var json = _redisStreamSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<CommandMessage>(json, _contractsOptions);

        // Assert.
        Assert.NotNull(roundTripped);
        AssertCommandMessagesAreStructurallyEqual(original, roundTripped);
    }

    [Fact]
    public void ContractsSerializes_RedisStreamDeserializes_PreservesAllFields()
    {
        // Arrange — scenario 2: Contracts library produces the wire bytes
        // (as a handler doing ad-hoc serialization would), RedisStream's
        // JsonMessageSerializer reads them back. The [JsonConverter]
        // attribute on Payload is honored by RedisStream's deserializer too.
        var original = BuildFullyPopulatedSample();

        // Act.
        var json = JsonSerializer.Serialize(original, _contractsOptions);
        var roundTripped = _redisStreamSerializer.Deserialize<CommandMessage>(json);

        // Assert.
        Assert.NotNull(roundTripped);
        AssertCommandMessagesAreStructurallyEqual(original, roundTripped);
    }

    [Fact]
    public void RedisStreamSerializes_ContractsDeserializes_NullAckIdRoundTripsAsNull()
    {
        // Arrange — exercise the WhenWritingNull divergence: RedisStream
        // OMITS the ackId field entirely from JSON when it's null. Contracts'
        // deserializer treats a missing property as null on a nullable type.
        var original = BuildFullyPopulatedSample() with { AckId = null };

        // Act.
        var json = _redisStreamSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<CommandMessage>(json, _contractsOptions);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.AckId);
        AssertCommandMessagesAreStructurallyEqual(original, roundTripped);

        // Sanity-check the WhenWritingNull behavior so a future RedisStream
        // version that changed this default would be caught here.
        Assert.DoesNotContain("ackId", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractsSerializes_RedisStreamDeserializes_NullAckIdRoundTripsAsNull()
    {
        // Arrange — Contracts library serializes "ackId": null explicitly.
        // RedisStream's deserializer must accept this form too.
        var original = BuildFullyPopulatedSample() with { AckId = null };

        // Act.
        var json = JsonSerializer.Serialize(original, _contractsOptions);
        var roundTripped = _redisStreamSerializer.Deserialize<CommandMessage>(json);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.AckId);
        AssertCommandMessagesAreStructurallyEqual(original, roundTripped);

        // Sanity-check that Contracts DOES emit the explicit null, so any
        // future change to the Contracts options that adopted WhenWritingNull
        // would be caught here (it would be a wire-format change requiring a
        // major version bump).
        Assert.Contains("\"ackId\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BothLibraries_RoundTripEmptyPayloadAsEmptyBase64String()
    {
        // Arrange — empty payload edge case: must serialize as "" (per
        // API ref §4 contract) and round-trip to ReadOnlyMemory<byte>.Empty.
        var original = BuildFullyPopulatedSample() with { Payload = ReadOnlyMemory<byte>.Empty };

        // Act — both directions.
        var jsonViaRedisStream = _redisStreamSerializer.Serialize(original);
        var viaContractsDeserializer =
            JsonSerializer.Deserialize<CommandMessage>(jsonViaRedisStream, _contractsOptions);

        var jsonViaContracts = JsonSerializer.Serialize(original, _contractsOptions);
        var viaRedisStreamDeserializer =
            _redisStreamSerializer.Deserialize<CommandMessage>(jsonViaContracts);

        // Assert — empty payload preserved both ways.
        Assert.NotNull(viaContractsDeserializer);
        Assert.True(viaContractsDeserializer.Payload.IsEmpty);

        Assert.NotNull(viaRedisStreamDeserializer);
        Assert.True(viaRedisStreamDeserializer.Payload.IsEmpty);

        // Spot-check that both libraries serialize empty Payload as "" not null.
        Assert.Contains("\"payload\":\"\"", jsonViaRedisStream, StringComparison.Ordinal);
        Assert.Contains("\"payload\":\"\"", jsonViaContracts, StringComparison.Ordinal);
    }
}
