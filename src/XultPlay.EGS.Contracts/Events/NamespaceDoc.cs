namespace XultPlay.EGS.Contracts.Events;

/// <summary>
/// Reserved namespace for future EGS-specific payload record types.
/// </summary>
/// <remarks>
/// <para>
/// This namespace ships empty in <c>XultPlay.EGS.Contracts</c> v1.0.0
/// and contains no public types. It is declared and reserved so that envelope types
/// (currently in <c>XultPlay.EGS.Contracts.Messaging</c>) and EGS-specific
/// payload types can evolve independently, and so that a future split of the envelope
/// types into a shared messaging package is a mechanical namespace rename rather than
/// a redesign.
/// </para>
/// <para>
/// Adding new types to this namespace is an additive (minor) SemVer change. Moving
/// types between <c>XultPlay.EGS.Contracts.Messaging</c> and this namespace
/// is breaking and requires a major version bump.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
