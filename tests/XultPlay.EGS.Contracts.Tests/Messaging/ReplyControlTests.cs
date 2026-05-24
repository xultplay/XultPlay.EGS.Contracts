using System.Collections.Generic;
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="ReplyControl"/>. Covers API Reference §8.1–5.
/// </summary>
public sealed class ReplyControlTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Hoisted per CA1861: array literals as constructor arguments would
    // allocate a fresh array on every call. CA1861 explicitly excludes
    // static-readonly initializers from its check.
    private static readonly string[] _roomsTournament42 = new[] { "tournament-42" };
    private static readonly string[] _roomsTournamentAndLobby = new[] { "tournament-42", "lobby-1" };
    private static readonly string[] _roomsLobby1 = new[] { "lobby-1" };

    /// <summary>
    /// Returns a freshly-allocated array on every call. Used by the
    /// "separate arrays are not record-equal" test that needs DISTINCT array
    /// references by design. Hides the array literal from CA1861 because the
    /// literal is no longer an argument — it's a method return value.
    /// </summary>
    private static string[] FreshLobby1Array() => new[] { "lobby-1" };

    [Fact]
    public void RoundTrip_RoomJoinWithThenEmit_PreservesAllValues()
    {
        // Arrange.
        var original = new ReplyControl(
            Kind: "room-join",
            Rooms: _roomsTournamentAndLobby,
            ThenEmit: "subscribed");

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<ReplyControl>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal("room-join", roundTripped.Kind);
        Assert.Equal(_roomsTournamentAndLobby, roundTripped.Rooms);
        Assert.Equal("subscribed", roundTripped.ThenEmit);
    }

    [Fact]
    public void RoundTrip_RoomLeaveWithoutThenEmit_PreservesAllValues()
    {
        // Arrange — ThenEmit is optional and may be null.
        var original = new ReplyControl(
            Kind: "room-leave",
            Rooms: _roomsTournament42,
            ThenEmit: null);

        // Act.
        var json = JsonSerializer.Serialize(original, _options);
        var roundTripped = JsonSerializer.Deserialize<ReplyControl>(json, _options);

        // Assert.
        Assert.NotNull(roundTripped);
        Assert.Equal("room-leave", roundTripped.Kind);
        Assert.Single(roundTripped.Rooms);
        Assert.Equal("tournament-42", roundTripped.Rooms[0]);
        Assert.Null(roundTripped.ThenEmit);
    }

    [Fact]
    public void Deserialize_JsonWithUnknownProperty_DoesNotThrow()
    {
        // Arrange — §8.4: unknown property tolerance.
        const string json = """
            {
              "kind": "room-join",
              "rooms": ["lobby"],
              "thenEmit": null,
              "xyz": 1
            }
            """;

        // Act.
        var result = JsonSerializer.Deserialize<ReplyControl>(json, _options);

        // Assert.
        Assert.NotNull(result);
        Assert.Equal("room-join", result.Kind);
    }

    [Fact]
    public void Serialize_RoomJoinSample_ProducesExpectedJson()
    {
        // Arrange — §8.5: golden wire format for the embedded "control" object.
        var sample = new ReplyControl(
            Kind: "room-join",
            Rooms: _roomsTournament42,
            ThenEmit: "subscribed");
        const string expected =
            "{" +
            "\"kind\":\"room-join\"," +
            "\"rooms\":[\"tournament-42\"]," +
            "\"thenEmit\":\"subscribed\"" +
            "}";

        // Act.
        var json = JsonSerializer.Serialize(sample, _options);

        // Assert.
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Equality_TwoRecordsBuiltFromSameArrayReference_AreEqual()
    {
        // Arrange — §8.3. When two ReplyControl records share the SAME
        // IReadOnlyList reference, record-generated Equals returns true.
        var a = new ReplyControl("room-join", _roomsLobby1, "subscribed");
        var b = new ReplyControl("room-join", _roomsLobby1, "subscribed");

        // Act + Assert.
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_TwoRecordsWithSeparateArrays_AreNotRecordEqual()
    {
        // Arrange — §8.3 (documentation test). When two ReplyControl records
        // hold SEPARATE arrays with equal contents, record-generated Equals
        // returns false because IReadOnlyList<string>.Equals is reference-based.
        // Consumers needing semantic equality must compare structurally.
        //
        // We use FreshLobby1Array() rather than an array literal so each
        // record holds a distinct reference — that's the whole point of the
        // test. Using a shared static readonly field here would defeat it.
        var a = new ReplyControl("room-join", FreshLobby1Array(), "subscribed");
        var b = new ReplyControl("room-join", FreshLobby1Array(), "subscribed");

        // Act + Assert.
        // Use EqualityComparer<T>.Default directly so we're invoking the
        // record's generated Equals method and not relying on xunit's
        // collection-iteration behavior for any nested IEnumerable.
        Assert.False(EqualityComparer<ReplyControl>.Default.Equals(a, b));

        // The structural pieces ARE equal element-wise — this documents the
        // intended use pattern for consumers that need semantic comparison.
        Assert.Equal(a.Kind, b.Kind);
        Assert.Equal(a.Rooms, b.Rooms);
        Assert.Equal(a.ThenEmit, b.ThenEmit);
    }
}
