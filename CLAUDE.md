# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status: MVP complete (M1–M6 done)

The solution exists (4 projects + tests, per the PRD architecture). Completed milestones:
- **M1** — scaffold, options binding (`KnxOptions` / `TuyaOptions` / `DeviceMappingOptions`), DI wiring.
- **M2** — Tuya local client (`Infrastructure/Tuya/`): persistent 3.3 socket per device on TuyaNet,
  heartbeat, background reconnect, pushed-STATUS read loop + interval poll → `DeviceReport` → aggregate.
- **M3** — KNX outbound / feedback path (`Infrastructure/Knx/`): domain events → status GA writes,
  `GroupValueRead` answered from the last known value, redundant-write dedup. (Light CCT status was
  originally excluded here and landed in M6.)
- **M4** — KNX inbound / command path (`Infrastructure/Knx/`): inbound `GroupValueWrite` on a mapped
  command GA → decoded (`KnxDpt` decoders) → routed via `BuildCommandBindings` → `KnxCommandTranslator`
  builds the existing Application command record → dispatched through MediatR `ISender` to the aggregate
  → Tuya `CONTROL`. Fan speed uses the DPT 3.007 dim-step (±1 level, break ignored). (Light CCT command
  was originally deferred here and landed in M6.) No `IKnxBus` port — the ACL dispatches inward via
  MediatR (matching M3's shape).
- **M5** — multi-device isolation + robust reconnect/backoff. Per-device isolation and Tuya exponential
  backoff already existed from M2; M5 hardened them: (1) a Tuya **liveness watchdog** (`TuyaConnection`)
  force-reconnects a stalled/half-open socket when no inbound byte arrives within `LivenessTimeoutSeconds`
  (UC-09 step 1 — not just TCP errors); (2) **KNX robust reconnect** — `KnxConnectionSupervisor` now runs a
  supervised connect→`WaitForDropAsync`→backoff loop (driven by `KnxBus.ConnectionStateChanged`), so a
  dropped bus re-attaches the inbound `GroupMessageReceived` subscription and the KNX→Tuya command path
  self-heals (previously it stayed dead until the next outbound write); (3) a shared, **jittered**
  `Infrastructure/Resilience/BackoffPolicy` used by both supervisors; (4) `TuyaConnectionSupervisor`
  guards each device's loop so one unexpected fault can't stop the host (FR-10). New `TuyaOptions`
  (`LivenessTimeoutSeconds`, `ConnectTimeoutSeconds`, `ReconnectInitial/MaxBackoffSeconds`) and `KnxOptions`
  (`ReconnectInitial/MaxBackoffSeconds`) tunables; the socket/bus loops stay manual-verified, backoff and
  `Device` connectivity transitions are unit-tested.

- **M6** — Light CCT (`Infrastructure/Knx/`): DP 23 (`temp_value`, 3 steps 0/500/1000) wired to KNX on
  both the status and command paths, using the shared percent DPT (**5.001 %**,
  `KnxDpt.Percent`/`DecodePercent`). Domain/Application/Tuya-ACL support already existed;
  M6 was purely the KNX-ACL wiring — added `Capability.LightCct` / `CommandCapability.LightCct`, the
  `StatusAddresses`/`CommandAddresses` yields, the `DeviceEventKnxHandler.Handle(LightCctChanged)` +
  its explicit DI registration, and the `KnxCommandTranslator` arm. Flicker mitigation (write DP 23
  only on an actual step change) lives in `Device.SetLightColourTemperature`; status is only ever
  published from authoritative device readback, so the resulting step (not the requested one) reaches
  KNX. Per-device disable via an empty GA still applies.

- **Post-MVP — Light dimming removed.** The Wind Calm hardware does not honour a brightness write
  (DP 22), so light **brightness/dimming was removed end-to-end**: the `LightBrightness` capability
  binding + key, the `Brightness` value object, `Device.SetLightBrightness`, the
  `SetLightBrightnessCommand`/handler, the `LightBrightness` command/report facade + snapshot fields,
  the dashboard `BrightnessPercent`/`BrightnessDp` DTO fields + UI (`%` number and dimming bar), and
  the `LightBrightness*` config keys are all gone. The light is **on/off (DP 20) + CCT (DP 23) only**.
  (UC-06 is kept as a doc, banner-marked *removed*.)

- **Post-MVP — Fan beep removed; DP 66 force-silenced on connect.** The confirmation beep (DP 66
  `fan_beep`) was briefly a full user-controllable `FanBeep` capability (KNX 1.001 command+status,
  dashboard 🔔/🔕 chip, `DesiredBeep` reconciliation), but the beep is only ever a nuisance — the device
  beeps on every LAN command while the RF remote is silent. So the whole capability was **removed
  end-to-end** (the `WindCalmCapabilities.FanBeep` key, `FanEndpoint.Beep`, the `DeviceCommand`/
  `DeviceReport` facades + snapshot field, `Device.SetFanBeep` + its `ApplyReportedState` branch, the
  `SetFanBeepCommand`/handler, the `WindCalmProfile` binding, the `FanDto.Beep` chip, the `DesiredBeep`
  option + `TuyaConnection` reconcile machinery, and the `FanBeep`/`FanBeepStatus` config keys are all
  gone). Replaced by a hidden **always-silence**: `DeviceProfile.OnConnectDps` (a raw dp→value map,
  empty by default) is set to `{"66": false}` by `WindCalmProfile`; on each (re)connect `TuyaConnection`
  reconciles it against the device's first state readback and writes **only the DPs whose value differs**
  (see the conditional-reconcile bullet below — originally a blind write, changed because a redundant
  write beeps). Inert for profiles that declare no `OnConnectDps`, and skipped on firmware that doesn't
  report DP 66 (e.g. the 3.4 `XW-FAN-215-D`, which must be muted in hardware). See UC-10.

- **Post-MVP — CCT step/cycle (long-press) added.** A KNX pushbutton can now *cycle* CCT via a relative
  **DPT 3.007** command, coexisting with the existing absolute-% `LightCct` command (5.001). Added a
  command-only `WindCalmCapabilities.LightCctStep` key + a *second* `WindCalmProfile` `CapabilityBinding`
  (no dps/status of its own — `CommandMappingKey = "LightCctStep"`, decodes via `KnxDpt.DecodeDimStep`,
  reuses DP 23's encoding/status), `StepLightCctCommand`/handler mirroring the other command/handlers, and
  `Device.CycleLightColourTemperature(up)` + `ColourTemperature.Cycle(up)` — index navigation over
  `Steps {0,500,1000}` that **wraps** at the rails (unlike fan speed, which clamps). Shipped GA `1/1/16`.
  Table-driven: no `KnxCommandTranslator`/`KnxBridge` changes. See UC-07 (07c).

- **Post-MVP — fan speed switched to absolute % (was relative 3.007 step).** Fan speed was originally
  exposed as a relative **DPT 3.007** dim-step command (`FanSpeedStep` GA, ±1 level, magnitude ignored)
  plus a raw **DPT 5.010** counter status carrying the level 0–6 (UC-02). A KNX visualization typed those
  group objects as **5.001 scaling %**, so the raw level byte read back as `round(level × 100 / 255) ≈ 1%`
  ("send 100, get 1%"), and a % slider couldn't set an absolute speed at all. Fixed by moving fan speed to
  **absolute DPT 5.001 %** on both paths and **dropping the relative step end-to-end**: the `FanSpeed`
  binding now decodes/encodes via the existing `KnxDpt.Percent`/`DecodePercent` (as `LightCct` already
  does); `SpeedLevel.FromPercent`/`ToPercent` do the %↔level 1–6 mapping (`ceil` in, `round` out,
  `0 %` = off); `StepFanSpeedCommand`/`Device.StepFanSpeed` were replaced by `SetFanSpeedCommand`/
  `Device.SetFanSpeedPercent` (`0 %` → power off, `1–100 %` → level + power-on if off, redundant → empty).
  The command mapping key was renamed `FanSpeedStep` → **`FanSpeed`** (GA `1/1/3` unchanged;
  `FanSpeedStatus` `1/1/4` unchanged, only its DPT changes). Table-driven — no `KnxDpt`/`KnxBridge`/
  `KnxCommandTranslator` structural changes; the dashboard still shows the raw 0–6 level. Config
  (`appsettings.json`, `.env.example`) and tests updated. See UC-02 (superseded banner).

- **Post-MVP — KNX boolean read-response fixed (DPT 1.001 1-bit sizing).** `GroupValueRead` on a status
  GA was already answered from the cached value (`KnxBridge.AnswerReadAsync`, FR-7/UC-08b), but **boolean
  status reads were never answered while multi-byte ones were.** Root cause (verified against the shipped
  `Knx.Falcon.Sdk 6.4.8671` assembly): `KnxDpt.Bool` returned a `byte[]` wrapped in `new GroupValue(byte[])`,
  which is **always 8-bit** (`SizeInBit == 8`, `new GroupValue(bool)` is 1-bit). DPT 1.001 must be a 1-bit
  "short" group value packed into the APCI octet; an actuator tolerates an oversized *write* but a reader
  discards an oversized *response*, so FanPower/FanDirection/LightPower/Availability writes worked yet their
  reads didn't (5.010 speed, 5.001 CCT, 7.006 timer were already correctly 8/16-bit and answered). Fix: the
  `KnxDpt` **encoders now return `GroupValue`** (`Bool → new GroupValue(bool)` etc.), and `GroupValue` is
  carried end-to-end — `CapabilityBinding.EncodeStatus : Func<CapabilityValue, GroupValue>`,
  `KnxStatusValue.Value : GroupValue?`, `KnxBridge.PublishAsync`/`WriteAsync`/`AnswerReadAsync` pass it
  straight to `WriteGroupValueAsync`/`RespondGroupValueAsync` (no `new GroupValue(byte[])` re-wrap), dedup
  compares `SizeInBit` + payload. Decoders stay `byte[]`. Every inbound group telegram and each read-answer
  outcome (answered / no-cached-value / unmapped GA) is logged (raise the level to see them — see the
  KNX-logging bullet below), and `AnswerReadAsync` captures `_bus` into a local to avoid a reconnect race.

- **Post-MVP — KNX per-telegram logging removed; state changes now logged at Information.** A live KNX
  bus is busy (many broadcasts on GAs the hub doesn't map); the per-telegram Debug logs still flooded
  `docker logs` whenever KNX was raised to `Debug` to trace commands. The pure bus-noise lines were
  **removed** (`KnxBridge`): the inbound-telegram trace (`KNX inbound … on …`), the "unmapped GA" read
  (now a silent ignore), and the "read answered" line — so even at `Debug` the noise is gone. What stays:
  **Warning** for real problems (write failed, bus dropped, read unanswered because bus down, command with
  no value, inbound-handler exception); **Debug** for the still-useful low-volume lines (outbound write,
  no-cached-value read gated to mapped GAs, inbound `KNX command … dispatched`); **Information** for
  connection lifecycle. Separately, **actual fan/light state changes are now logged at `Information`** from
  `DeviceStateIngestionService.ReportStateAsync` — it iterates the `DeviceCapabilityChanged` events
  `Device.ApplyReportedState` returns (already change-filtered — one line per genuine change, plus a
  one-time first-report baseline burst, never per poll) and logs `Device {Device} {Capability} changed to
  {Value}.`. So at the default level `docker logs` shows connect/disconnect, genuine errors, and every
  fan/light state change.

- **Post-MVP — 3.5 heartbeat frame fixed (was flapping the connection every ~10s).** The hand-rolled
  session codec's `TuyaSessionCodec.BuildHeartbeat` sent an encrypted **`"{}"`** body for the periodic
  `HEART_BEAT` (cmd 9). A 3.5 unit (`VentilatorVictor`) **closed the socket on receipt of every
  heartbeat**, so the connection dropped ~10s after each connect and reconnected in a loop — which also
  re-ran the `OnConnectDps` `{66:false}` baseline write on every reconnect (the "beeps on reconnect"
  report), and under load the rapid socket churn exhausted the module's ~3-socket cap so *new* connects
  timed out (looked like "can't connect", initially misread as KNX-flood thread starvation). Root cause
  confirmed empirically: with the heartbeat disabled the link is rock-stable; my live probe never sent a
  heartbeat so it was never exercised. Fix: `BuildHeartbeat` now sends the same **`{"gwId","devId"}`**
  identifying body tinytuya uses (and that `BuildQuery` already sends) — an empty payload also works but
  the `{gwId,devId}` form matches the reference and the device's accepted query shape. `HEART_BEAT` stays
  in the no-version-header set (unprefixed, session-key encrypted). Regression test added
  (`TuyaSessionCodecTests.Heartbeat_carries_the_gwId_devId_identifying_body`). Also fixed alongside: a
  `TuyaConnection.ConnectAsync` **socket leak** — a failed/timed-out connect never reached `_client` so
  `CloseSocket()` couldn't dispose it, leaking a socket per failed attempt against the ~3-socket cap; it
  now disposes the `TcpClient` in a `catch`. Note: a single **startup** reconnect can still occur on a
  rapid restart (leftover device-side socket from the killed instance not yet reaped) — intermittent,
  self-heals in ~1s, not the baseline write's doing (verified: clean startups occur with and without it).

- **Post-MVP — `OnConnectDps` baseline is now a conditional reconcile, not a blind write (kills the
  reconnect beep).** Live testing showed the 3.5 `VentilatorVictor` **beeps on *any* DP 66 write — even a
  redundant `{66:false}` when the buzzer is already off** (the connect itself is silent; the write is what
  beeps). Since Victor's DP 66 already persists `false`, the old blind on-connect write silenced nothing
  yet beeped on every (re)connect. Fix: `TuyaConnection` no longer writes `OnConnectDps` blindly at connect.
  It arms `_baselineReconciled=false`, and on the **first state report** (from the connect `DP_QUERY`)
  `ReconcileBaselineAsync` writes only the DPs whose **current reported value differs** from the baseline
  (`ComputeBaselineWrites`, compared via invariant string form). So: already-off buzzer → **no write, no
  beep, ever** (both the user's devices); buzzer genuinely on → one write to correct it (the single
  intended beep), then quiet. A DP the device doesn't report (3.4 `XW-FAN-215-D`, no DP 66) is skipped.
  Logs `already satisfied; no write sent` or `wrote connect-time baseline DPs [...] (state differed)` at
  **Information** so it's confirmable from `docker logs`. Unit tests: `TuyaConnectionBaselineTests`
  (already-satisfied → no write; wrong state → writes only the differing DP; unreported DP → skipped).

- **Post-MVP — session codec (3.4/3.5) no longer sends a heartbeat at all (the real flap fix; supersedes
  the `{gwId,devId}` body change above).** The earlier `{gwId,devId}` `BuildHeartbeat` body was a hypothesis
  never exercised live — and live logs later showed `VentilatorVictor` (3.5) **still** flapping (connect →
  device-side close → reconnect ~every heartbeat) with it. The one thing actually proven was *"heartbeat
  disabled → link rock-stable."* So the 3.5 firmware rejects the `HEART_BEAT` frame regardless of body. Fix:
  `ITuyaCodec` gains `UsesHeartbeat`; `TuyaSessionCodec` returns **`false`** (both 3.4 and 3.5),
  `TuyaNetCodec` returns **`true`** (3.1/3.3 heartbeat is library-proven). `TuyaConnection.RunAsync` only
  starts `HeartbeatLoopAsync` when the codec opts in (a `Task.Delay(Timeout.Infinite)` placeholder keeps the
  `WhenAny`/`WhenAll(Swallow(...))` shape uniform). 3.4/3.5 keepalive + the liveness watchdog are carried by
  the existing 10s `DP_QUERY` poll (proven to round-trip; 10s poll < 30s liveness < ~30s module idle-drop).
  `BuildHeartbeat` is kept (interface + regression test) but off the hot path. Why the drop was invisible in
  logs: the read-loop close throws into `Task.WhenAny`, which `Swallow(...)` discards, so it surfaced as a
  clean `offline; reconnecting` with no `connection error` warning. `HeartbeatIntervalSeconds` now applies
  only to the TuyaNet codec. Tests: `TuyaSessionCodecTests.Session_codec_does_not_use_a_heartbeat`
  (3.4/3.5 → false) and `TuyaNet_codec_uses_a_heartbeat` (3.1/3.3 → true).

- **Post-MVP — KNX NAT / TCP tunnelling knobs (Docker "connects but buttons don't work" fix).** KNXnet/IP
  tunnelling advertises a *local* IP in the tunnel's HPAI; Falcon auto-picks it. On a single-homed Windows
  dev box that's correct, but in **Docker with host networking on a multi-homed host** (docker0/bridge/VPN
  interfaces) Falcon can advertise the **wrong** local IP: the handshake completes (bus logs `Connected`)
  yet the connection-state heartbeat and **inbound group telegrams go to an unreachable address**, so the
  bus **flaps** (repeated `Connecting`/`Connected`, no error between) and the **KNX→Tuya command path is
  dead** while everything else looks healthy — the canonical "works in VS, not in Docker" symptom. Fix:
  two new `KnxOptions` — **`UseNat`** (bool, default false; empty HPAI 0.0.0.0 → gateway replies to the
  real UDP source) and **`Protocol`** (`Auto`|`Udp`|`Tcp`, default `Auto`; `Tcp` = KNXnet/IP-v2 tunnelling,
  no return-path problem at all, most robust in Docker when the gateway supports it) — wired into
  `KnxBridge.OpenBusAsync` via `IpTunnelingConnectorParameters.UseNat`/`ProtocolType` (`ParseProtocol`).
  Defaults preserve the prior behaviour (VS unchanged); the container should set `KnxOptions__UseNat=true`
  (see `.env.example`). Verified against the shipped `Knx.Falcon.Sdk 6.4.8671` (`IpProtocol {Auto,Udp,Tcp}`).

- **Post-MVP — dashboard KNX pill now tracks the bus in real time.** The pill reads
  `KnxBridge.IsConnected` at snapshot-**publish** time, and snapshots were only published on startup and
  on device state-change events — so a KNX connect that happened *between* device events (e.g. after the
  gateway's tunnel slots freed up post-startup) left the pill stuck on "disconnected" until the next
  device change. `KnxConnectionSupervisor` now calls `DashboardSnapshotPublisher.PublishCurrent()` after
  each connect, drop, and failed connect, so the pill flips immediately. The bridge can't publish itself
  (the publisher already depends on `KnxBridge` → DI cycle); the supervisor is the one place that observes
  both transitions. The refresh is guarded (a publish fault can't stall the reconnect loop) and inert when
  the dashboard is disabled.

The MVP is functionally complete. Future work is general hardening. When implementing, follow the PRD's
declared architecture and milestones rather than inventing your own.

**Read first, always:** `docs/PRD-MVP.md` is the source of truth for what to build. It resolves
every major design decision (Tuya client library, KNX transport, CCT scope, REST/WS scope) — do not
re-open those unless asked.

## What tuya-hub is

A C# (**.NET 10**) console/worker app: a **generic Tuya → KNX hub**. Each Tuya device is controlled
**locally over the LAN** (Tuya local protocol, per-device local key, **no cloud**) and exposed as KNX
group objects for bidirectional control + status feedback. Device types are pluggable via **profiles**
(see the architecture section); the **CREATE / IKOHS "Windcalm"** ceiling-fan-with-light (protocol
**3.3**) is the **first and currently only profile** — so it's the one concrete implementation, not the
whole product.

The MVP is deliberately narrow: statically-configured devices only (**no LAN discovery**, no cloud,
no REST/WebSocket *control* — those are documented in `docs/use-cases/` as future/secondary surface
but are explicitly out of MVP scope). A **read-only** status dashboard was added post-MVP (see below);
it is status feedback only and does not open a control surface. The dashboard also shows a live list of
Tuya devices **discovered** broadcasting on the LAN but not yet configured (passive UDP listen only —
no control, no config writes).

## Architecture (Domain-Driven Design — see PRD §10)

**DDD**, four projects mapping onto DDD's layered architecture (dependencies point inward:
Domain ← Application ← Infrastructure/Host). Reuse DsmrHub's cross-cutting patterns — options binding,
byte encoding/endianness, KNX read-response behavior, MediatR — but the **domain model, not the wire
protocols, is the center**.

**The strategic key: both KNX and Tuya are external models behind Anti-Corruption Layers.** The domain
never sees a `dps` dict or a `GroupValue` byte array; each ACL is the only place its foreign model is
translated. When implementing, keep protocol types out of `Domain`/`Application`.

**Device profiles (the genericity seam).** Everything specific to a device *type* lives in one
`DeviceProfile` (a table of `CapabilityBinding`s) under `Infrastructure/Profiles/`: its Tuya DP numbers +
wire codecs, its KNX DPT/mapping-key bindings, and its dashboard presentation. The shared engines iterate
that table, so **adding a device type is registering a profile — no shared ACL file is edited.** Concretely:
- The domain currency is generic: `DeviceCommand`/`DeviceReport` are capability-keyed bags (`CapabilityKey`)
  behind Wind Calm's typed facade (`FanPower`, …), and a single `DeviceCapabilityChanged` event replaced the
  per-capability events. `CapabilityValue` is the domain-neutral scalar it carries.
- `TuyaProfileCodec` (replaced the old `TuyaDatapoints`), the KNX `KnxBridge` store/command builders,
  `KnxCommandTranslator`, the single `DeviceEventKnxHandler`, and the dashboard projection all read the
  profile bindings instead of a hard-coded fan/light switch. The old `Capability`/`CommandCapability` enums
  are gone.
- A device declares its type via `TuyaOptions.Devices[].Profile` (default `"wind-calm"`), resolved by
  `IDeviceProfileRegistry` (plus `ConfiguredDeviceProfiles` for the name→profile lookup the KNX ACL needs).
- **`WindCalmProfile` is profile #1.** Its `Device` aggregate still owns the fan+light rules (dim-step, CCT
  flicker, MCU timer) — those stay concrete, not data-driven. A new device type = a new profile + its own
  aggregate/Application command records (MediatR auto-discovers the handlers) + one registration line in
  `AddInfrastructure`.

- **TuyaHub.Domain** — pure model, no framework/protocol deps.
  - **Aggregate root `Device`** (one Wind Calm unit; the consistency boundary) with entities
    **`FanEndpoint`** / **`LightEndpoint`**. **All invariants and state transitions live here**, not in
    Application — the dim-step rules, speed clamp 1–6, CCT steps, MCU timer.
  - **Value objects**: `DeviceId`, `LocalKey`, `ProtocolVersion`, `GroupAddress`, `SpeedLevel`,
    `ColourTemperature`, `Direction`, `Timer` — self-validating, immutable.
  - **Domain events** (`DeviceStateChanged`, `FanSpeedChanged`, `DeviceWentOffline`, …) dispatched via
    MediatR. **Registry port** `IDeviceRegistry`.
- **TuyaHub.Application** — thin application services / command handlers; orchestrate use cases,
  **delegate all rules to the aggregate**. Owns the ports the ACLs implement (`IDeviceGateway` for Tuya,
  `IKnxBus` for KNX). No business logic. Shape it so REST/WS can be added later without rework.
- **TuyaHub.Infrastructure** — adapters implementing the ports; the two ACLs.
  - `Knx/` — **KNX ACL**: `KnxBridge` on **Knx.Falcon.Sdk** (`IpTunnelingConnectorParameters`,
    `GroupValueWrite`/`RespondGroupValueAsync`), telegrams ↔ domain.
  - `Tuya/` — **Tuya ACL**: local client, `dps` ↔ domain datapoints.
  - `Options/` — `KnxOptions`, `TuyaOptions`, `DeviceMappingOptions`, config-backed `IDeviceRegistry`.
- **TuyaHub** (host) — `WebApplication` host (SDK `Microsoft.NET.Sdk.Web`); composition root, DI via
  `AddApplication()` + `AddInfrastructure(configuration)`, plus the read-only web dashboard
  (`Dashboard/DashboardEndpoints.cs` + `wwwroot/index.html`). The background services still run as
  hosted services on the same host. When `DashboardOptions.Enabled=false` no HTTP endpoint is mapped.

**Tuya local client decision (resolved, PRD §10a):** inside the Tuya ACL, adopt **`TuyaNet` by ClusterM**
(NuGet) for the **3.1/3.3** codec/framing/AES — do **not** hand-roll protocol 3.3. Build a thin reliability
layer on top that TuyaNet lacks: **heartbeat (0x09)**, **background reconnect with backoff**, and a
**pushed-STATUS read loop** (TuyaNet is poll-only). Note TuyaNet is **GPL-3.0** and pulls in Newtonsoft.Json.

**Protocol versions & the codec seam.** All wire concerns sit behind `ITuyaCodec` (`Infrastructure/Tuya/Codec/`),
selected per device by `TuyaCodecFactory` from the configured `ProtocolVersion`. Two implementations:
`TuyaNetCodec` (3.1/3.3, delegating to TuyaNet) and **`TuyaSessionCodec` (3.4/3.5, hand-rolled)** — TuyaNet
is 3.3-max and no maintained .NET library does 3.4/3.5. The hand-rolled codec (ported from tinytuya
`PROTOCOL.md`, using built-in `System.Security.Cryptography`) performs the mandatory session-key handshake
(`SESS_KEY_NEG_START/RESP/FINISH`) that 3.4/3.5 require before any DP traffic, then 3.4's `55AA` + HMAC-SHA256
+ session-keyed AES-ECB or 3.5's `6699` + AES-GCM framing (`TuyaFrame`/`TuyaCrypto`). `TuyaConnection` is now
transport-only: it owns the socket + reliability layer, calls `NegotiateSessionAsync` on every (re)connect,
and routes all framing through the codec. `TuyaProfileCodec` (dps ↔ domain) is version-agnostic and unchanged.

## Configuration model (`appsettings.json`)

Three bound options sections (PRD §7): `KnxOptions`, `TuyaOptions` (a list of devices — IP, deviceId,
localKey, protocolVersion, and an optional **`Profile`** device-type id defaulting to `"wind-calm"`),
and `DeviceMappings` (per-device, keyed by `TuyaOptions.Devices[].Name`). Each `DeviceMappings` entry is
now a plain **capability-mapping-key → GA** dictionary (`DeviceMapping : Dictionary<string,string>`); the
valid keys (`FanPower`, `FanPowerStatus`, …) are the ones the device's profile declares. Group
addresses are held as **strings**; a **missing or empty GA string disables that function**. Command and
status GAs are always separate. No rebuild required to add/remap a device.

A fourth section, **`DashboardOptions`** (`Enabled` default true in the shipped `appsettings.json`,
`Port` 8080), gates the read-only web dashboard. When `Enabled=false` the host binds no HTTP endpoint
and behaves like the original worker. `DashboardOptions.Enabled` **also gates LAN discovery**: when the
dashboard is off, the Tuya UDP discovery listener is not started and no discovery port is bound.

LAN discovery uses our own `TuyaLanDiscoveryListener` (`Infrastructure/Tuya/`), not TuyaNet's
`TuyaScanner`: the scanner decoded beacons on a library-owned thread that **rethrew** any decode failure,
so a single undecodable datagram (a protocol-3.5 `00 00 66 99` beacon, or junk UDP) crashed the whole
host. The listener binds the same ports (6666/6667) and decodes each packet inside a `try/catch` so bad
beacons are logged and skipped: `00 00 55 AA` beacons (3.1–3.4) via TuyaNet's codec (`internal TuyaParser`,
cached reflection delegate — the PRD forbids hand-rolling 3.3), and `00 00 66 99` beacons (3.5) via the
hand-rolled `TuyaFrame.Parse6699` AES-GCM path. Both use the universal UDP key, so **3.5 devices are now
discoverable** too.

## Domain constraints that will bite you (from the Wind Calm use cases)

These are firmware/protocol quirks the code must respect — see `docs/use-cases/wind-calm/README.md`:

- **DP 62 (fan speed) must be sent as an integer `1..6`, never a string** — the #1 cause of silent
  speed failures.
- **Fan power (DP 60) and speed (DP 62) are independent DPs**, but the KNX % command spans both:
  `0 %` turns the fan off (writes DP 60 = false), `1–100 %` sets a level (and turns the fan on if it
  was off).
- Fan speed uses KNX **DPT 5.001 %** (absolute) for **both** command and status: command
  `ceil(% / 100 × 6)` → level 1–6 (`0 %` = off); status level 1–6 → `round(level × 100 / 6)` %
  (`0 %` = off). See the Post-MVP bullet below.
- **Timer (DP 64) is MCU-owned** — the bridge sets/reads remaining minutes; it never counts down locally.
- **CCT (DP 23)** exposes only 3 discrete steps (0/500/1000); writing it can flicker the light — write
  only on an actual step change.
- **RF-remote changes do not reliably push** a `dps` update — feedback needs periodic polling in addition
  to pushed status.
- The module accepts only **~3 concurrent TCP sockets** on port 6668 — keep one persistent socket per
  device; don't run the Smart Life app concurrently.

## Documentation conventions

- Use cases live in `docs/use-cases/` (top-level, cross-device) and `docs/use-cases/wind-calm/`
  (per-capability for the one supported device). Each `UC-NN-*.md` follows the template in
  `docs/use-cases/README.md`.
- Per the user's global preference: after any change, check whether these `.md` files need updating
  and update them in the same task.

## Environment

- Shell is **PowerShell** (pwsh 7+) on Windows; use PowerShell syntax, one command per invocation.
- Once code exists, expect standard .NET tooling: `dotnet build`, `dotnet test`
  (`dotnet test --filter "FullyQualifiedName~<Name>"` for a single test), `dotnet run`.

## Running in Docker

The host ships as a container (`Dockerfile`, `docker-compose.yml`, `.env.example` at repo root).

- **Image**: multi-stage — `dotnet/sdk:10.0` publishes only `TuyaHub/TuyaHub.csproj` (tests
  excluded), `dotnet/aspnet:10.0` runs `TuyaHub.dll` as the non-root `app` user. (aspnet, not
  runtime: the host serves the dashboard on Kestrel and needs the ASP.NET shared framework; `publish`
  copies `wwwroot/` physically into the image so the static page serves in Production.)
- **Networking**: `network_mode: host` (Linux LAN host). Required because KNXnet/IP tunnelling
  (UDP 3671) needs the gateway's UDP replies routed back to the client, which Docker bridge NAT
  breaks; Tuya local is outbound TCP 6668. Host networking is **also** what lets Tuya UDP discovery
  beacons (broadcast on UDP 6666/6667) reach the container — the host firewall must allow inbound
  UDP 6666/6667 for the "Discovered" dashboard list to populate. Both target fixed LAN IPs — no ports
  are published. The dashboard is reachable at `http://<host>:8080/` over the host network
  (`DashboardOptions__Port`).
- **Config**: supplied via env vars (double-underscore binding), not baked into the image. Copy
  `.env.example` → `.env` (git-ignored), set device secrets (`LocalKey`) and the `DeviceMappings`
  group addresses (keyed by device `Name`). The shipped `appsettings.json` keeps everything
  `Enabled=false`; the `.env` enables and configures devices at runtime.
- **Run**: `docker compose pull && docker compose up -d` pulls the published image and starts it
  (compose consumes `ghcr.io/thomasgodon/tuya-hub:latest`). For local dev without a pull, build
  first: `docker build -t tuya-hub:local .` then
  `docker run --rm --network host --env-file .env tuya-hub:local`.

## Releasing (versioned Docker publish)

Deployment images are published to **GitHub Container Registry** by
`.github/workflows/docker-publish.yml`, mirroring the DsmrHub sibling repo. There is no version in
any `.csproj` — the git tag *is* the version. To cut a release:

```pwsh
git tag v0.1.0
git push origin v0.1.0
```

Pushing a `v*` tag is the sole trigger. The workflow builds the root `Dockerfile` (linux/amd64) and
pushes `ghcr.io/thomasgodon/tuya-hub` tagged `{version}` (e.g. `0.1.0`), `{major}.{minor}` (`0.1`),
and `latest` — via `docker/metadata-action`, authenticated with the built-in `GITHUB_TOKEN`
(`packages: write`), so no secret setup is needed.

**One-time step:** the first publish creates a **private** GHCR package. Make it public
(GitHub → repo → Packages → tuya-hub → Package settings) or `docker login ghcr.io` on the LAN host
before `docker compose pull` can fetch it.
