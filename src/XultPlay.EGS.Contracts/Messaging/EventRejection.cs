namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// The payload of an <c>event-rejected</c> Socket.IO event the bridge emits
/// back to a client when it refuses to dispatch a command.
/// </summary>
/// <remarks>
/// Direction: bridge → client.
/// Wire format: this record IS the Socket.IO event payload; changing the
/// shape breaks the client UI.
/// </remarks>
/// <param name="Event">
/// The event name that was rejected (e.g. <c>"slot-spin"</c>).
/// </param>
/// <param name="Reason">
/// One of the constants in <see cref="RejectionReason"/>.
/// </param>
/// <param name="TraceId">
/// 32-char lowercase hex, for client-side log correlation.
/// </param>
/// <param name="RetryAfterMs">
/// Optional. When non-null, hints how long the client should wait before
/// retrying. The bridge sets this for rate-limit rejections (computed from
/// the token bucket); null otherwise.
/// </param>
public sealed record EventRejection(
    string Event,
    string Reason,
    string TraceId,
    int? RetryAfterMs);
