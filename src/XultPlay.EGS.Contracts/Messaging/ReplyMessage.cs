using System;
using System.Text.Json.Serialization;

namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// The envelope a handler publishes to <c>results:{ListenerHost}</c> for the
/// bridge to deliver back to a specific socket.
/// </summary>
/// <remarks>
/// Direction: handler → bridge.
/// Stream: <c>results:{ListenerHost}</c> (echoed from the originating
/// <see cref="CommandMessage"/>).
/// Consumed by: the bridge's reply handler.
/// </remarks>
/// <param name="TargetSid">
/// The Socket.IO sid to deliver to. Equals the original
/// <see cref="CommandMessage.SocketIoSid"/> for normal replies.
/// </param>
/// <param name="TraceId">
/// Echoed from the original command for correlation.
/// </param>
/// <param name="EventName">
/// The Socket.IO event name to emit to the client. Must be in the outbound
/// allowlist enforced by the bridge.
/// </param>
/// <param name="Payload">
/// Opaque payload bytes for the client. The bridge forwards verbatim and
/// does NOT re-serialize. Exposed as <see cref="ReadOnlyMemory{T}"/> so the
/// contents cannot be mutated through the property (CA1819). Base64-encoded
/// on the wire.
/// </param>
/// <param name="AckId">
/// When non-null, the bridge emits a Socket.IO ACK frame
/// (<c>43{id}[payload]</c>) instead of an EVENT frame (<c>42[...]</c>). Must
/// equal the original <see cref="CommandMessage.AckId"/>.
/// </param>
/// <param name="SideEffect">
/// Optional post-emit instruction. Currently recognized: <c>"disconnect"</c>.
/// </param>
/// <param name="Control">
/// Optional pre-emit control directive — see <see cref="ReplyControl"/>.
/// </param>
public sealed record ReplyMessage(
    string TargetSid,
    string TraceId,
    string EventName,
    [property: JsonConverter(typeof(ReadOnlyMemoryByteJsonConverter))]
    ReadOnlyMemory<byte> Payload,
    string? AckId,
    string? SideEffect,
    ReplyControl? Control);
