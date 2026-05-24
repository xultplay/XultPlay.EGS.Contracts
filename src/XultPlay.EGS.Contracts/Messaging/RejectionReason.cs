namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// Canonical string constants for <see cref="EventRejection.Reason"/>.
/// </summary>
/// <remarks>
/// <para>
/// The values on the wire are <c>snake_case</c> — the client UI parses these
/// strings directly, so changing them is a breaking change.
/// </para>
/// <para>
/// <c>ServerDraining</c> and <c>UnknownEvent</c> are reserved: the bridge does
/// NOT emit them in v1.0.0, but the constants are defined now so adding them
/// later is an additive (minor) change rather than a breaking one.
/// </para>
/// </remarks>
public static class RejectionReason
{
    /// <summary>
    /// The bridge rejected the event because an identical request is already
    /// in-flight for the same socket. The client should not retry; the original
    /// is still being processed.
    /// </summary>
    public const string DuplicateInFlight = "duplicate_in_flight";

    /// <summary>
    /// The bridge rejected the event because the per-socket token bucket is
    /// empty. <see cref="EventRejection.RetryAfterMs"/> hints when to retry.
    /// </summary>
    public const string RateLimitExceeded = "rate_limit_exceeded";

    /// <summary>
    /// Reserved for v1.x. Will indicate the bridge pod is draining (shutting
    /// down) and the client should reconnect. Not emitted in v1.0.0.
    /// </summary>
    public const string ServerDraining = "server_draining";

    /// <summary>
    /// Reserved for v1.x. Will indicate the event name was not in the
    /// inbound allowlist. Not emitted in v1.0.0.
    /// </summary>
    public const string UnknownEvent = "unknown_event";
}
