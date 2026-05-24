using System;
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="CommandMessage"/>. Covers API Reference §8.1–5.
/// </summary>
public sealed class CommandMessageTests
{
    // Cached options per CA1869. Default System.Text.Json options — the
    // per-property [JsonConverter] attribute on Payload pulls in the
    // ReadOnlyMemoryByteJsonConverter for the byte field automatically.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static CommandMessage SampleFromSpec()
    {
        // Mirrors the §7 example. Note timestamp serialization: the spec
        // example shows "...Z" but System.Text.Json's default DateTimeOffset
        // output is "+00:00". Per Q7, consumers must tolerate both. Inputs
        // with either form parse to the same DateTimeOffset.
        return new CommandMessage(
            Type: "slot-spin",
            SocketIoSid: "abc123def456ghi789jkl012",
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            AccessToken: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            ClientId: "web-1",
            GameType: "slot",
            ListenerHost: "bridge-prod-3",
            PayloadEncoding: "encrypted",
            Payload: Convert.FromBase64String("U2FsdGVkX1+vupppZksvRf5pq5g5XjFRIipRkwB0K1Y96Qsv2L3svw=="),
            AckId: null,
            ReceivedAt: new DateTimeOffset(2026, 5, 22, 14, 30, 45, 123, TimeSpan.Zero));
    }

    [Fact]
    public void RoundTrip_AllFieldsSet_PreservesAllValues()
    {
        // Arrange.
        var original = SampleFromSpec();

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<CommandMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal(original.Type, roundTripped.Type);
        Assert.Equal(original.SocketIoSid, roundTripped.SocketIoSid);
        Assert.Equal(original.TraceId, roundTripped.TraceId);
        Assert.Equal(original.AccessToken, roundTripped.AccessToken);
        Assert.Equal(original.ClientId, roundTripped.ClientId);
        Assert.Equal(original.GameType, roundTripped.GameType);
        Assert.Equal(original.ListenerHost, roundTripped.ListenerHost);
        Assert.Equal(original.PayloadEncoding, roundTripped.PayloadEncoding);
        Assert.True(original.Payload.Span.SequenceEqual(roundTripped.Payload.Span));
        Assert.Equal(original.AckId, roundTripped.AckId);
        Assert.Equal(original.ReceivedAt, roundTripped.ReceivedAt);
    }

    [Fact]
    public void RoundTrip_NullAckId_RemainsNull()
    {
        // Arrange — §8.1: AckId is the only nullable on CommandMessage.
        var original = SampleFromSpec() with { AckId = null };

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<CommandMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.AckId);
    }

    [Fact]
    public void RoundTrip_NonNullAckId_PreservesValue()
    {
        // Arrange.
        var original = SampleFromSpec() with { AckId = "42" };

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<CommandMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal("42", roundTripped.AckId);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_RemainsEmpty()
    {
        // Arrange — §8.2: Payload MAY be empty for events with no body.
        var original = SampleFromSpec() with { Payload = ReadOnlyMemory<byte>.Empty };

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<CommandMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.Payload.IsEmpty);
    }

    [Fact]
    public void Deserialize_JsonWithUnknownProperty_DoesNotThrow()
    {
        // Arrange — §8.4: unknown property tolerance for forward-compat.
        // System.Text.Json's default is to ignore unknown properties.
        const string json = """
            {
              "type": "slot-spin",
              "socketIoSid": "abc123",
              "traceId": "9f86d081884c7d659a2feaa0c55ad015",
              "accessToken": "tok",
              "clientId": "web-1",
              "gameType": "slot",
              "listenerHost": "bridge-1",
              "payloadEncoding": "plain",
              "payload": "",
              "ackId": null,
              "receivedAt": "2026-05-22T14:30:45.123+00:00",
              "xyz": 1
            }
            """;

        // Act.
        var result = JsonSerializer.Deserialize<CommandMessage>(json, _options);

        // Assert.
        Assert.NotNull(result);
        Assert.Equal("slot-spin", result.Type);
    }

    [Fact]
    public void Deserialize_SpecExampleWithZulu_ParsesUtcTimestamp()
    {
        // Arrange — §7 example uses "...Z" for the timestamp; consumers must
        // tolerate it (per Q7). This test verifies input compatibility even
        // though our serialization output uses "+00:00".
        const string json = """
            {
              "type": "slot-spin",
              "socketIoSid": "abc123def456ghi789jkl012",
              "traceId": "9f86d081884c7d659a2feaa0c55ad015",
              "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
              "clientId": "web-1",
              "gameType": "slot",
              "listenerHost": "bridge-prod-3",
              "payloadEncoding": "encrypted",
              "payload": "U2FsdGVkX1+vupppZksvRf5pq5g5XjFRIipRkwB0K1Y96Qsv2L3svw==",
              "ackId": null,
              "receivedAt": "2026-05-22T14:30:45.123Z"
            }
            """;

        // Act.
        var result = JsonSerializer.Deserialize<CommandMessage>(json, _options);

        // Assert — value is UTC midnight-offset, regardless of Z vs +00:00 input form.
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.ReceivedAt.Offset);
        Assert.Equal(new DateTime(2026, 5, 22, 14, 30, 45, 123, DateTimeKind.Unspecified), result.ReceivedAt.DateTime);
    }

    [Fact]
    public void Serialize_GoldenSample_ProducesExpectedJson()
    {
        // Arrange — §8.5: golden wire format. The expected JSON differs from
        // the §7 example in ONE way that is NOT a wire-protocol change:
        // DateTimeOffset with zero offset serializes as "+00:00" rather than
        // "Z" (Q7 accepted this default; consumers must tolerate both forms).
        // The payload's '+' character is preserved literally — our converter
        // uses Utf8JsonWriter.WriteBase64StringValue which bypasses the
        // JavaScriptEncoder, matching what XultPlay.RedisStream produces.
        var sample = SampleFromSpec();

        // Use a single-line compact form to match System.Text.Json's default output.
        const string expected =
            "{" +
            "\"type\":\"slot-spin\"," +
            "\"socketIoSid\":\"abc123def456ghi789jkl012\"," +
            "\"traceId\":\"9f86d081884c7d659a2feaa0c55ad015\"," +
            "\"accessToken\":\"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"," +
            "\"clientId\":\"web-1\"," +
            "\"gameType\":\"slot\"," +
            "\"listenerHost\":\"bridge-prod-3\"," +
            "\"payloadEncoding\":\"encrypted\"," +
            "\"payload\":\"U2FsdGVkX1+vupppZksvRf5pq5g5XjFRIipRkwB0K1Y96Qsv2L3svw==\"," +
            "\"ackId\":null," +
            "\"receivedAt\":\"2026-05-22T14:30:45.123+00:00\"" +
            "}";

        // Act.
        var json = JsonSerializer.Serialize(sample, _options);

        // Assert.
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Equality_TwoRecordsWithSameFieldValues_AreStructurallyEqual()
    {
        // Arrange — §8.3. Note: record equality is reference-based for
        // ReadOnlyMemory<byte>, so we compare structurally instead.
        var a = SampleFromSpec();
        var b = SampleFromSpec();

        // Act + Assert.
        Assert.Equal(a.Type, b.Type);
        Assert.Equal(a.SocketIoSid, b.SocketIoSid);
        Assert.Equal(a.TraceId, b.TraceId);
        Assert.Equal(a.AccessToken, b.AccessToken);
        Assert.Equal(a.ClientId, b.ClientId);
        Assert.Equal(a.GameType, b.GameType);
        Assert.Equal(a.ListenerHost, b.ListenerHost);
        Assert.Equal(a.PayloadEncoding, b.PayloadEncoding);
        Assert.True(a.Payload.Span.SequenceEqual(b.Payload.Span));
        Assert.Equal(a.AckId, b.AckId);
        Assert.Equal(a.ReceivedAt, b.ReceivedAt);
    }
}
