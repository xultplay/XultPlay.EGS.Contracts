# XultPlay.EGS.Contracts

[![nuget](https://img.shields.io/badge/GitHub%20Packages-1.0.0-blue?logo=nuget)](https://github.com/orgs/xultplay/packages)
[![target](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)


Shared record types defining the wire contract between the components of the
XultPlay EGS (eGame System) fleet — the WebSocket bridge, message handlers,
the notifier, and the EGS bot bridge. This is a thin, dependency-free library
intended to change rarely. It contains only data types — no behaviour, no I/O,
and no references to other XultPlay libraries.

**What this library is NOT**: a base for building handlers, a client SDK, a
serializer registry, or a place for helper methods. Cryptography, session
registry, logging EventIds, stream-name constants, and routing configuration
all live elsewhere. Anything beyond pure data should be added to a consumer
library, not here.

## Namespace layout

```
XultPlay.EGS.Contracts.dll
├── XultPlay.EGS.Contracts.Messaging   ← envelope types  (POPULATED in v1.0.0)
└── XultPlay.EGS.Contracts.Events      ← EGS payload types (RESERVED, empty in v1.0.0)
```

The split is deliberate. Envelope types (`CommandMessage`, `ReplyMessage`,
`BroadcastMessage`, `EventRejection`, etc.) are transport infrastructure that
isn't EGS-specific — if a second business domain emerges later, they'll move
to a shared messaging package (likely `XultPlay.RedisStream` v1.2.0 or a new
`XultPlay.WebSocketBridge.Messaging`). Pre-creating the `.Messaging`
sub-namespace makes that future split a mechanical `using`-rename rather than
a redesign. The `.Events` namespace is reserved for the decrypted,
deserialized payload shapes that EGS handlers will eventually receive
(`SlotSpinPayload`, `SubscribePayload`, ...); v1.0.0 ships it empty. See the
API reference §5 and §6.1 for the full rationale.

## Quick start

This package is published to GitHub Packages under the `xultplay` org, not to
nuget.org.

### 1. Configure your machine (one-time)

Generate a GitHub Personal Access Token with the `read:packages` scope at
<https://github.com/settings/tokens>, then register the feed:

```cmd
dotnet nuget add source https://nuget.pkg.github.com/xultplay/index.json ^
  --name github-xultplay ^
  --username YOUR_GITHUB_USERNAME ^
  --password YOUR_PAT ^
  --store-password-in-clear-text
```

Verify with `dotnet nuget list source`. See the XultPlay engineering
playbook §13 for the full per-developer setup.

### 2. Configure your repo (one-time per consuming repo)

Drop this `nuget.config` at the consuming repo's root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org"        value="https://api.nuget.org/v3/index.json"            protocolVersion="3" />
    <add key="github-xultplay"  value="https://nuget.pkg.github.com/xultplay/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="github-xultplay">
      <package pattern="XultPlay.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

If the consuming repo lives in the `xultplay` GitHub org, CI uses the
auto-provided `GITHUB_TOKEN` to read packages — no secret setup is needed.

### 3. Add the package

```cmd
dotnet add package XultPlay.EGS.Contracts --version 1.0.0
```

Always pin to a specific version. Never use floating ranges.

## Usage

### Serializing a `CommandMessage`

The records define their wire-format converter via per-field attribute, so no
options-level converter registration is required. Default
`JsonSerializerOptions` with `JsonNamingPolicy.CamelCase` are enough:

```csharp
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

var command = new CommandMessage(
    Type:           "slot-spin",
    SocketIoSid:    "abc123...",
    TraceId:        Guid.NewGuid().ToString("N"),
    AccessToken:    "eyJ...",
    ClientId:       "web-1",
    GameType:       "slot",
    ListenerHost:   "bridge-prod-3",
    PayloadEncoding:"encrypted",
    Payload:        Convert.FromBase64String("U2FsdGVkX1+..."),
    AckId:          null,
    ReceivedAt:     DateTimeOffset.UtcNow);

string json = JsonSerializer.Serialize(command, options);
CommandMessage? parsed = JsonSerializer.Deserialize<CommandMessage>(json, options);
```

The `Payload` field round-trips as a base64 JSON string. Encryption is opaque
to this library — handlers decrypt after deserializing, based on
`PayloadEncoding`.

### Registering the converter for ad-hoc `ReadOnlyMemory<byte>` serialization

If your own types (outside this library) use `ReadOnlyMemory<byte>` and you
want them to serialize the same way, register the converter at the options
level instead of (or alongside) the attribute:

```csharp
using XultPlay.EGS.Contracts.Messaging;

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new ReadOnlyMemoryByteJsonConverter() },
};

// Any ReadOnlyMemory<byte> field on any type now uses this converter,
// even without [JsonConverter] attribute decoration.
```

The converter writes via `Utf8JsonWriter.WriteBase64StringValue`, which is
byte-identical to the .NET 8 built-in `ReadOnlyMemory<byte>` serialization
path that `XultPlay.RedisStream` uses internally. A regression test in the
test project (`Write_MatchesBuiltInReadOnlyMemoryByteFormat`) pins that
guarantee — if anyone in a future version routes the bytes through the
`JavaScriptEncoder` and breaks wire compatibility with RedisStream, that test
will fail.

### Types shipped in v1.0.0

All in the `XultPlay.EGS.Contracts.Messaging` namespace. See the API
reference §3 for full field-by-field semantics, the wire format, and the
governing producer/consumer for each type.

| Type | Direction | Purpose |
|---|---|---|
| `CommandMessage` | Bridge → Handler | Carries a client event from the bridge to whichever handler the routing config selects. Includes session, trace id, opaque payload, and origin metadata. |
| `ReplyMessage` | Handler → Bridge | Carries a handler's response back to a specific socket, plus optional control (`ReplyControl`) and post-emit side effects. |
| `ReplyControl` | embedded | Optional pre-emit directive in a `ReplyMessage` — currently used for room-join / room-leave. |
| `BroadcastMessage` | Notifier/Handler → Bridge | Fan-out envelope: targets a room or every socket on a bridge pod. |
| `EventRejection` | Bridge → Client | Payload of an `event-rejected` Socket.IO event when the bridge refuses to dispatch a command. |
| `RejectionReason` | static class | Canonical `snake_case` string constants for `EventRejection.Reason` — frozen wire values. |
| `ReadOnlyMemoryByteJsonConverter` | converter | Serializes `ReadOnlyMemory<byte>` as base64. Required for any payload-bearing field. |

## Versioning policy

This library follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
strictly:

- **Additive changes** — new optional fields (always nullable, always with a
  default value), new types, new constants, new types in `.Events` — bump
  the minor version (`1.0.0` → `1.1.0`).
- **Breaking changes** — renamed fields, removed fields, type changes, or
  moving a type between `.Messaging` and `.Events` — bump the major version
  (`1.x.0` → `2.0.0`). Avoid.

Optional fields are added by appending with a default:

```csharp
public sealed record CommandMessage(
    // ... existing fields ...
    string? NewField = null);   // added in v1.1.0, compiles against old call sites
```

Tagged releases (`v1.x.y`) drive CI publication to GitHub Packages — see
`.github/workflows/publish.yml`. Pre-releases use SemVer suffixes
(`v1.1.0-beta.1`).

For the full versioning rules and the deferred-refactor path into a shared
messaging library, see the API reference §6.

## Testing

The test project covers all 7 shipped types: 54 unit and integration tests
across 8 test files. Coverage includes JSON round-trip, null/empty/edge
cases, unknown-property tolerance, golden wire-format tests against the API
reference §7 examples, and a `RedisStreamWireCompatTests` suite that
verifies bidirectional compatibility with `XultPlay.RedisStream`'s
`JsonMessageSerializer` (API reference §8 test #11 / open question Q8).

Run the tests with:

```cmd
dotnet test --configuration Release
```

## License

Internal — see repository for licensing terms.

## Related

- API reference — `docs/XultPlay.EGS.Contracts-API-Reference-v1_0_0.docx` (generated post-publish; see Phase 5 in the build plan)
- [XultPlay.RedisStream](https://github.com/xultplay/XultPlay.RedisStream) — the producer / consumer infrastructure this contract flows through
