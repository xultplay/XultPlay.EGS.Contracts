using System;
using System.Text.Json.Serialization;

namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// The envelope a notifier or handler deposits on
/// <c>broadcasts:{ListenerHost}</c> when an event needs to fan out to a room
/// or to every socket on a bridge pod.
/// </summary>
/// <remarks>
/// <para>
/// Direction: notifier or handler → bridge.
/// Stream: <c>broadcasts:{ListenerHost}</c>.
/// </para>
/// <para>
/// Validation requirements:
/// <c>Target</c> MUST be <c>"room"</c> or <c>"all"</c> (case-sensitive).
/// When <c>Target == "room"</c>, <c>Room</c> MUST be non-null and non-empty.
/// When <c>Target == "all"</c>, <c>Room</c> SHOULD be null (the bridge
/// ignores it either way).
/// </para>
/// </remarks>
/// <param name="TraceId">
/// 32-char lowercase hex; correlates the broadcast across all pods that fan
/// it out.
/// </param>
/// <param name="EventName">
/// The Socket.IO event name emitted to all matching sockets. Must be in the
/// outbound allowlist.
/// </param>
/// <param name="Payload">
/// Opaque payload bytes for the client. The bridge forwards verbatim.
/// Exposed as <see cref="ReadOnlyMemory{T}"/> so the contents cannot be
/// mutated through the property (CA1819). Base64-encoded on the wire.
/// </param>
/// <param name="Target">
/// Targeting strategy for this pod: <c>"room"</c> (use <see cref="Room"/>)
/// or <c>"all"</c> (emit to every local socket on this pod).
/// </param>
/// <param name="Room">
/// When <see cref="Target"/> is <c>"room"</c>, the room name. Null otherwise.
/// </param>
/// <param name="SideEffect">
/// Optional post-emit instruction. Currently recognized:
/// <c>"disconnect_after_emit"</c> (close affected sockets after emit; used
/// by maintenance kicks).
/// </param>
public sealed record BroadcastMessage(
    string TraceId,
    string EventName,
    [property: JsonConverter(typeof(ReadOnlyMemoryByteJsonConverter))]
    ReadOnlyMemory<byte> Payload,
    string Target,
    string? Room,
    string? SideEffect);
