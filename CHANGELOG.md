# Changelog

All notable changes to **XultPlay.EGS.Contracts** are documented here.

The format is based on [Keep a Changelog v1.1.0](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_No unreleased changes._

## [1.0.0] — 2026-05-24

Initial release.

### Added

Wire-contract record types (namespace `XultPlay.EGS.Contracts.Messaging`):

- `CommandMessage` — bridge → handler envelope. Carries event type, session
  identifiers, opaque payload, and origin metadata.
- `ReplyMessage` — handler → bridge envelope. Routes a response back to a
  specific socket, with optional control directive and post-emit side effect.
- `ReplyControl` — embedded in `ReplyMessage` for pre-emit actions
  (currently `room-join`, `room-leave`).
- `BroadcastMessage` — fan-out envelope targeting a room or every socket on a
  bridge pod.
- `EventRejection` — payload of the `event-rejected` Socket.IO event.
- `RejectionReason` — canonical `snake_case` string constants:
  `duplicate_in_flight`, `rate_limit_exceeded`, `server_draining` (reserved),
  `unknown_event` (reserved).
- `ReadOnlyMemoryByteJsonConverter` — `System.Text.Json` converter that
  serializes `ReadOnlyMemory<byte>` as base64. Implemented via
  `Utf8JsonWriter.WriteBase64StringValue` to produce byte-identical output to
  `XultPlay.RedisStream`'s built-in `ReadOnlyMemory<byte>` serialization
  path. Required for any field of type `ReadOnlyMemory<byte>`.

Reserved namespace:

- `XultPlay.EGS.Contracts.Events` — present but intentionally empty. Reserved
  for future EGS-specific payload record types (e.g. `SlotSpinPayload`).
  Pre-creating the sub-namespace establishes the convention for the eventual
  split into a shared messaging library; see API reference §6.1.

### Design notes

- **Two-namespace layout.** The library is structured as
  `XultPlay.EGS.Contracts.Messaging` (transport-level envelope types,
  populated) and `XultPlay.EGS.Contracts.Events` (EGS-specific payload types,
  reserved). If a second business domain emerges, envelopes can move to a
  shared messaging package via a mechanical `using` rewrite — no record
  shapes change and no wire format changes.
- **Zero runtime dependencies** beyond the .NET 8 BCL. No reference to
  `XultPlay.RedisStream`, `XultPlay.EGS.*`, `StackExchange.Redis`, logging
  libraries, or ASP.NET Core. AOT/trim-safe.
- **camelCase wire format** matching `XultPlay.RedisStream`'s
  `JsonMessageSerializer` default. Unknown properties tolerated for forward
  compatibility. Optional fields use nullable types with default values so
  new fields can be added in v1.x.0 without breaking older consumers.
- **`Utf8JsonWriter.WriteBase64StringValue`** is used for byte payloads
  rather than `WriteStringValue(Convert.ToBase64String(...))`. The two paths
  produce different output for the same bytes — the latter routes through
  the configured `JavaScriptEncoder` and escapes `+` to `\u002B`. RedisStream
  uses the former path internally, so this library matches.

### Test coverage

54 tests across 8 test files:

- 12 tests for `ReadOnlyMemoryByteJsonConverter` (empty, null, invalid base64,
  64 KB payload, random binary content, full byte-range coverage, golden
  base64 sequence, and a sentinel test that pins byte-identical output
  against the built-in `ReadOnlyMemory<byte>` serialization path).
- 42 tests across the 6 message types — JSON round-trip, optional-field
  handling, empty payload, unknown property tolerance, equality semantics,
  and golden wire-format assertions against the API reference §7 examples.
- 5 tests in `RedisStreamWireCompatTests` that verify
  `XultPlay.RedisStream.Serialization.JsonMessageSerializer` and this
  library round-trip records to each other's output in both directions. This
  positively closes open question Q8 (RedisStream wire-format compatibility)
  before publish.

### Build configuration

- Targets `net8.0`.
- Treats warnings as errors; analyzer mode `All`; StyleCop and
  Microsoft.CodeAnalysis.NetAnalyzers enabled per the `xultplay/csharp-template`
  configuration.
- `Microsoft.SourceLink.GitHub` enabled for downstream source-debugging by
  package consumers.
- Symbols published as `.snupkg` alongside the `.nupkg`.

[Unreleased]: https://github.com/xultplay/XultPlay.EGS.Contracts/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/xultplay/XultPlay.EGS.Contracts/releases/tag/v1.0.0
