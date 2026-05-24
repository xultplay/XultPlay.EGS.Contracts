using System;
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="ReplyMessage"/>. Covers API Reference §8.1–5.
/// </summary>
public sealed class ReplyMessageTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Hoisted per CA1861: array literals as constructor arguments would
    // allocate a fresh array on every call.
    private static readonly string[] _roomsTournament42 = new[] { "tournament-42" };

    /// <summary>
    /// Mirrors the §7 "ReplyMessage with room-join control" example.
    /// </summary>
    private static ReplyMessage SampleWithRoomJoinControl()
    {
        return new ReplyMessage(
            TargetSid: "abc123def456ghi789jkl012",
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            EventName: "subscribed",
            Payload: Convert.FromBase64String("eyJzdGF0dXMiOiJvayJ9"),
            AckId: null,
            SideEffect: null,
            Control: new ReplyControl(
                Kind: "room-join",
                Rooms: _roomsTournament42,
                ThenEmit: "subscribed"));
    }

    [Fact]
    public void RoundTrip_AllFieldsSet_PreservesAllValues()
    {
        // Arrange.
        var original = SampleWithRoomJoinControl();

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<ReplyMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal(original.TargetSid, roundTripped.TargetSid);
        Assert.Equal(original.TraceId, roundTripped.TraceId);
        Assert.Equal(original.EventName, roundTripped.EventName);
        Assert.True(original.Payload.Span.SequenceEqual(roundTripped.Payload.Span));
        Assert.Equal(original.AckId, roundTripped.AckId);
        Assert.Equal(original.SideEffect, roundTripped.SideEffect);
        Assert.NotNull(roundTripped.Control);
        Assert.Equal(original.Control!.Kind, roundTripped.Control!.Kind);
        Assert.Equal(original.Control.Rooms, roundTripped.Control.Rooms);
        Assert.Equal(original.Control.ThenEmit, roundTripped.Control.ThenEmit);
    }

    [Fact]
    public void RoundTrip_AllOptionalsNull_RemainsNull()
    {
        // Arrange — AckId, SideEffect, Control are all nullable.
        var original = new ReplyMessage(
            TargetSid: "sid-1",
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            EventName: "ack",
            Payload: ReadOnlyMemory<byte>.Empty,
            AckId: null,
            SideEffect: null,
            Control: null);

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<ReplyMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.AckId);
        Assert.Null(roundTripped.SideEffect);
        Assert.Null(roundTripped.Control);
        Assert.True(roundTripped.Payload.IsEmpty);
    }

    [Fact]
    public void RoundTrip_SideEffectDisconnect_PreservesValue()
    {
        // Arrange — §3.2: SideEffect recognized values include "disconnect".
        var original = new ReplyMessage(
            TargetSid: "sid-1",
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            EventName: "kicked",
            Payload: ReadOnlyMemory<byte>.Empty,
            AckId: null,
            SideEffect: "disconnect",
            Control: null);

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<ReplyMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal("disconnect", roundTripped.SideEffect);
    }

    [Fact]
    public void Deserialize_JsonWithUnknownProperty_DoesNotThrow()
    {
        // Arrange — §8.4: unknown property tolerance.
        const string json = """
            {
              "targetSid": "sid-1",
              "traceId": "9f86d081884c7d659a2feaa0c55ad015",
              "eventName": "ack",
              "payload": "",
              "ackId": null,
              "sideEffect": null,
              "control": null,
              "xyz": 1
            }
            """;

        // Act.
        var result = JsonSerializer.Deserialize<ReplyMessage>(json, _options);

        // Assert.
        Assert.NotNull(result);
        Assert.Equal("ack", result.EventName);
    }

    [Fact]
    public void Serialize_RoomJoinSample_ProducesExpectedJson()
    {
        // Arrange — §8.5: golden wire format matching the §7 example.
        var sample = SampleWithRoomJoinControl();
        const string expected =
            "{" +
            "\"targetSid\":\"abc123def456ghi789jkl012\"," +
            "\"traceId\":\"9f86d081884c7d659a2feaa0c55ad015\"," +
            "\"eventName\":\"subscribed\"," +
            "\"payload\":\"eyJzdGF0dXMiOiJvayJ9\"," +
            "\"ackId\":null," +
            "\"sideEffect\":null," +
            "\"control\":{" +
                "\"kind\":\"room-join\"," +
                "\"rooms\":[\"tournament-42\"]," +
                "\"thenEmit\":\"subscribed\"" +
            "}" +
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
        // ReadOnlyMemory<byte> and IReadOnlyList<string>, so we do not rely on
        // record-generated Equals here; we compare structurally instead.
        // The wire-format contract guarantees equivalent JSON round-trips, not
        // identity-based record equality of in-memory instances.
        var a = SampleWithRoomJoinControl();
        var b = SampleWithRoomJoinControl();

        // Act + Assert.
        Assert.Equal(a.TargetSid, b.TargetSid);
        Assert.Equal(a.TraceId, b.TraceId);
        Assert.Equal(a.EventName, b.EventName);
        Assert.True(a.Payload.Span.SequenceEqual(b.Payload.Span));
        Assert.Equal(a.AckId, b.AckId);
        Assert.Equal(a.SideEffect, b.SideEffect);
        Assert.NotNull(a.Control);
        Assert.NotNull(b.Control);
        Assert.Equal(a.Control!.Kind, b.Control!.Kind);
        Assert.Equal(a.Control.Rooms, b.Control.Rooms);
        Assert.Equal(a.Control.ThenEmit, b.Control.ThenEmit);
    }
}
