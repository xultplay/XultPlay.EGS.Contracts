using System;
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="BroadcastMessage"/>. Covers API Reference §8.1–5.
/// </summary>
public sealed class BroadcastMessageTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Mirrors the §7 "BroadcastMessage targeting a room" example.
    /// </summary>
    private static BroadcastMessage SampleTargetRoom()
    {
        return new BroadcastMessage(
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            EventName: "tournament-update",
            Payload: Convert.FromBase64String("eyJzY29yZSI6MTAwfQ=="),
            Target: "room",
            Room: "tournament-42",
            SideEffect: null);
    }

    /// <summary>
    /// Mirrors the §7 "BroadcastMessage targeting all sockets on the pod
    /// (with disconnect side effect)" example.
    /// </summary>
    private static BroadcastMessage SampleTargetAllWithDisconnect()
    {
        return new BroadcastMessage(
            TraceId: "9f86d081884c7d659a2feaa0c55ad015",
            EventName: "kick-all-users",
            Payload: Convert.FromBase64String("eyJyZWFzb24iOiJtYWludGVuYW5jZSJ9"),
            Target: "all",
            Room: null,
            SideEffect: "disconnect_after_emit");
    }

    [Fact]
    public void RoundTrip_TargetRoom_PreservesAllValues()
    {
        // Arrange.
        var original = SampleTargetRoom();

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<BroadcastMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal(original.TraceId, roundTripped.TraceId);
        Assert.Equal(original.EventName, roundTripped.EventName);
        Assert.True(original.Payload.Span.SequenceEqual(roundTripped.Payload.Span));
        Assert.Equal("room", roundTripped.Target);
        Assert.Equal("tournament-42", roundTripped.Room);
        Assert.Null(roundTripped.SideEffect);
    }

    [Fact]
    public void RoundTrip_TargetAllWithNullRoom_PreservesNulls()
    {
        // Arrange — when Target == "all", Room is null.
        var original = SampleTargetAllWithDisconnect();

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<BroadcastMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal("all", roundTripped.Target);
        Assert.Null(roundTripped.Room);
        Assert.Equal("disconnect_after_emit", roundTripped.SideEffect);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_RemainsEmpty()
    {
        // Arrange.
        var original = SampleTargetRoom() with { Payload = ReadOnlyMemory<byte>.Empty };

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<BroadcastMessage>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.Payload.IsEmpty);
    }

    [Fact]
    public void Deserialize_JsonWithUnknownProperty_DoesNotThrow()
    {
        // Arrange — §8.4: unknown property tolerance.
        const string json = """
            {
              "traceId": "9f86d081884c7d659a2feaa0c55ad015",
              "eventName": "tournament-update",
              "payload": "",
              "target": "room",
              "room": "lobby",
              "sideEffect": null,
              "xyz": 1
            }
            """;

        // Act.
        var result = JsonSerializer.Deserialize<BroadcastMessage>(json, _options);

        // Assert.
        Assert.NotNull(result);
        Assert.Equal("tournament-update", result.EventName);
    }

    [Fact]
    public void Serialize_TargetRoomSample_ProducesExpectedJson()
    {
        // Arrange — §8.5: golden wire format for the §7 room-target example.
        var sample = SampleTargetRoom();
        const string expected =
            "{" +
            "\"traceId\":\"9f86d081884c7d659a2feaa0c55ad015\"," +
            "\"eventName\":\"tournament-update\"," +
            "\"payload\":\"eyJzY29yZSI6MTAwfQ==\"," +
            "\"target\":\"room\"," +
            "\"room\":\"tournament-42\"," +
            "\"sideEffect\":null" +
            "}";

        // Act.
        var json = JsonSerializer.Serialize(sample, _options);

        // Assert.
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Serialize_TargetAllWithDisconnectSample_ProducesExpectedJson()
    {
        // Arrange — §8.5: golden wire format for the §7 all-target example
        // with the disconnect side effect.
        var sample = SampleTargetAllWithDisconnect();
        const string expected =
            "{" +
            "\"traceId\":\"9f86d081884c7d659a2feaa0c55ad015\"," +
            "\"eventName\":\"kick-all-users\"," +
            "\"payload\":\"eyJyZWFzb24iOiJtYWludGVuYW5jZSJ9\"," +
            "\"target\":\"all\"," +
            "\"room\":null," +
            "\"sideEffect\":\"disconnect_after_emit\"" +
            "}";

        // Act.
        var json = JsonSerializer.Serialize(sample, _options);

        // Assert.
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Equality_TwoRecordsWithSameFieldValues_AreStructurallyEqual()
    {
        // Arrange — §8.3. ReadOnlyMemory<byte> is reference-based; structural compare.
        var a = SampleTargetRoom();
        var b = SampleTargetRoom();

        // Act + Assert.
        Assert.Equal(a.TraceId, b.TraceId);
        Assert.Equal(a.EventName, b.EventName);
        Assert.True(a.Payload.Span.SequenceEqual(b.Payload.Span));
        Assert.Equal(a.Target, b.Target);
        Assert.Equal(a.Room, b.Room);
        Assert.Equal(a.SideEffect, b.SideEffect);
    }
}
