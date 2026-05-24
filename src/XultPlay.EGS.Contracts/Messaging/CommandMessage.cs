using System;
using System.Text.Json.Serialization;

namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// The envelope written by the bridge to a per-event command stream and consumed
/// by an EGS handler.
/// </summary>
/// <remarks>
/// <para>
/// Direction: bridge → handler.
/// Stream: <c>slot:commands:{event}</c> (or whatever the routing config names).
/// Acked by: the handler's <c>IRedisProducer.PublishAsync</c> returning
/// successfully on <c>results:{ListenerHost}</c>.
/// </para>
/// <para>
/// Validation requirements (producer's responsibility; consumers may assume):
/// <c>Type</c>, <c>SocketIoSid</c>, <c>TraceId</c>, <c>ClientId</c>, <c>GameType</c>,
/// <c>ListenerHost</c>, <c>PayloadEncoding</c> are non-null and non-empty.
/// <c>AccessToken</c> MAY be empty for events with <c>authRequired: none</c>.
/// <c>Payload</c> MAY be empty (zero-length, not null) for events with no body.
/// <c>AckId</c> is null when Socket.IO ACK callbacks aren't in use.
/// </para>
/// </remarks>
/// <param name="Type">
/// Canonical message-type name (typically matches the routing-config's
/// <c>messageType</c>; falls back to the raw event name). Used by handlers to
/// discriminate multi-event handlers (e.g. an EGS BetControl handler that
/// handles both <c>bet-up</c> and <c>bet-down</c>).
/// </param>
/// <param name="SocketIoSid">
/// The Socket.IO sid of the originating connection. The handler echoes it
/// back in <see cref="ReplyMessage.TargetSid"/> so the bridge knows who to
/// deliver the reply to.
/// </param>
/// <param name="TraceId">
/// 32-char lowercase hex (<c>Guid.NewGuid().ToString("N")</c>). Propagated
/// end-to-end for correlated logging. The handler MUST echo it in
/// <see cref="ReplyMessage.TraceId"/>.
/// </param>
/// <param name="AccessToken">
/// Session token. Handler validates via the session registry; the bridge
/// does NOT validate.
/// </param>
/// <param name="ClientId">
/// Caller-supplied client identifier (e.g. <c>web-1</c>, <c>mobile-3</c>).
/// </param>
/// <param name="GameType">
/// The game family this connection belongs to (e.g. <c>slot</c>, <c>live</c>,
/// <c>bingo</c>).
/// </param>
/// <param name="ListenerHost">
/// The bridge pod that owns the originating socket. The handler MUST publish
/// its reply to <c>results:{ListenerHost}</c>.
/// </param>
/// <param name="PayloadEncoding">
/// Either <c>"encrypted"</c> or <c>"plain"</c>, from the routing rule. Tells
/// the handler whether to decrypt before parsing the payload.
/// </param>
/// <param name="Payload">
/// The opaque payload bytes sliced from the original WebSocket receive buffer.
/// Exposed as <see cref="ReadOnlyMemory{T}"/> so the contents cannot be
/// mutated through the property (CA1819). The bridge does NOT decrypt or
/// re-serialize. Base64-encoded on the wire.
/// </param>
/// <param name="AckId">
/// Optional Socket.IO ACK callback id. The handler must include it in
/// <see cref="ReplyMessage.AckId"/> for ACK delivery.
/// </param>
/// <param name="ReceivedAt">
/// Wall-clock UTC timestamp from the bridge — when the frame arrived.
/// </param>
public sealed record CommandMessage(
    string Type,
    string SocketIoSid,
    string TraceId,
    string AccessToken,
    string ClientId,
    string GameType,
    string ListenerHost,
    string PayloadEncoding,
    [property: JsonConverter(typeof(ReadOnlyMemoryByteJsonConverter))]
    ReadOnlyMemory<byte> Payload,
    string? AckId,
    DateTimeOffset ReceivedAt);
