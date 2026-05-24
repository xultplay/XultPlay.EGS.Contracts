using System.Collections.Generic;

namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// Optional control instructions embedded in a <see cref="ReplyMessage"/>.
/// Tells the bridge to perform a pre-emit action (e.g. join or leave a room)
/// before any subsequent event emission.
/// </summary>
/// <remarks>
/// Validation: <c>Rooms</c> must be non-empty; <c>Kind</c> is case-sensitive.
/// </remarks>
/// <param name="Kind">
/// Currently recognized values: <c>"room-join"</c>, <c>"room-leave"</c>.
/// Case-sensitive.
/// </param>
/// <param name="Rooms">
/// Room names involved in the control. Exposed as
/// <see cref="IReadOnlyList{T}"/> so callers cannot mutate the contents
/// (CA1819). The bridge updates its registry and emits
/// <c>SADD rooms:{name} {listenerHost}</c> for each room.
/// </param>
/// <param name="ThenEmit">
/// Optional event name to emit AFTER the control action completes
/// (e.g. <c>"subscribed"</c> after a successful <c>room-join</c>).
/// </param>
public sealed record ReplyControl(
    string Kind,
    IReadOnlyList<string> Rooms,
    string? ThenEmit);
