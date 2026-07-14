# PRD — tuya-hub MVP: KNX bridge for Tuya Wind Calm

**Status:** Draft
**Owner:** Thomas Godon
**Date:** 2026-07-09
**Related:** [Use cases](use-cases/README.md) · [Wind Calm use cases](use-cases/wind-calm/README.md)

---

## 1. Summary

`tuya-hub` is a C# (.NET 10) console/worker application that bridges **CREATE / IKOHS
"Windcalm"** ceiling-fan-with-light devices to a **KNX** installation. Each Tuya device is
controlled **locally over the LAN** (Tuya local protocol 3.3, per-device local key — no cloud
dependency) and exposed as **KNX group objects** so it can be driven from, and report state back
to, the KNX bus.

The MVP explicitly **does not discover devices on the LAN**. Every device is declared statically
in `appsettings.json` (IP, device id, local key, protocol version) and each device's functions are
mapped to KNX group addresses in configuration — following the exact configuration-binding pattern
already used in the **DsmrHub** project (`KnxOptions` + a group-address mapping dictionary bound via
`IOptions<>`).

## 2. Goals

- **G1** — Control one or more Wind Calm devices from KNX: fan power, fan speed, fan direction, fan
  timer, light power, light brightness, light CCT.
- **G2** — Report device state back to KNX on separate status group addresses.
- **G3** — Configure everything from `appsettings.json`: a static list of devices + per-device
  group-address mapping. No LAN scan, no cloud.
- **G4** — Reuse the DsmrHub architecture and KNX integration approach (Knx.Falcon.Sdk, options
  binding, byte encoding/endianness, read-response behavior) so the two hubs stay consistent.

## 3. Non-goals (MVP)

- LAN discovery / auto-detection of devices (deferred — see [UC-01](use-cases/UC-01-discover-tuya-devices.md)).
- Tuya Cloud connectivity (local protocol only).
- REST API and WebSocket streaming (the top-level use cases describe these as a secondary surface;
  **out of scope for the MVP**, but the architecture must not preclude them).
- Device types other than Wind Calm (`fsd`, protocol 3.3).
- ~~A web dashboard/UI.~~ **Added post-MVP:** a **read-only** status dashboard (single static
  page + Server-Sent Events, served on Kestrel, gated by `DashboardOptions`) now lists every
  configured device with its live state. This is status feedback only — it does **not** add REST/WS
  device *control*, which remains out of scope.
- Obtaining local keys (a manual, one-time operator task via the Tuya IoT platform).

## 4. Users & context

| Actor | Interest |
|-------|----------|
| **Operator** (installer / homeowner) | Declares devices + GAs in config; expects the fan/light to respond from wall switches / KNX visualisation. |
| **KNX installation** | Sends commands and reads status via group addresses. |
| **tuya-hub** | Keeps a persistent local connection to each device, translates both directions. |

## 5. Device model (from the Wind Calm use cases)

One physical unit = **two logical endpoints** (fan + light), controlled independently.

| Group | DP | Code | Type | Range | Meaning |
|-------|----|------|------|-------|---------|
| Fan | 60 | `fan_switch` | bool | true/false | Fan on/off |
| Fan | 62 | `fan_speed` | **int** | **1–6** | Speed level (**send as integer, never string**) |
| Fan | 63 | `fan_direction` | enum | `forward`/`reverse` | Direction (summer/winter) |
| Fan | 64 | `countdown_left_fan` | int | 0–540 min | Auto-off timer (**counted down by device MCU**) |
| Light | 20 | `switch_led` | bool | true/false | Light on/off |
| Light | 22 | `bright_value` | int | 0–1000 | Brightness (scaled) |
| Light | 23 | `temp_value` | int | 0/500/1000 | Colour temperature (**optional — firmware flicker bug**) |

Protocol facts that constrain the design: TCP **6668**, single persistent socket, heartbeat every
~9–15 s, `DP_QUERY 0x0a` to read all DPs, `CONTROL 0x07` to write, AES-128-ECB with `55AA` framing.
Module accepts only **~3 concurrent sockets** — keep one persistent socket per device and don't run
the Smart Life app simultaneously.

## 6. KNX group-object model

Command (KNX→Tuya) and feedback (Tuya→KNX) use **separate group addresses** per function.

| Function | DP | Command GA (DPT) | Status GA (DPT) |
|----------|----|------------------|-----------------|
| Fan power | 60 | 1.001 switch | 1.001 |
| Fan speed | 62 | **3.007** dim step (up/down) | **5.010** count (1–6; 0 = off) |
| Fan direction | 63 | 1.001 (0 = forward/summer, 1 = reverse/winter) | 1.001 |
| Fan timer | 64 | 7.006 minutes | 7.006 remaining |
| Light power | 20 | 1.001 switch | 1.001 |
| Light brightness | 22 | 5.001 % | 5.001 % |
| Light CCT | 23 | 5.001 % → 3 steps | 5.001 % |

**Speed is relative:** the bridge receives 4-bit dim telegrams (DPT 3.007) and walks the level
`1..6`; because a 3.007 object carries no feedback, current speed is reported on a separate 5.010
status GA. Adopted rule: *dim-up from off* → turn fan on (DP 60) **and** set level 1; *dim-down at
level 1* → stay at 1 (fan is switched off only via the power object).

## 7. Configuration design (`appsettings.json`)

Mirrors DsmrHub: options classes bound with `services.Configure<T>(configuration.GetSection(...))`,
group addresses held as **strings** so an empty/unset value binds cleanly and is parsed lazily.
Difference from DsmrHub: tuya-hub has **multiple devices**, so the mapping is nested per device and
distinguishes **command** vs **status** GAs.

```jsonc
{
  "KnxOptions": {
    "Enabled": true,
    "Host": "192.168.0.10",          // KNXnet/IP gateway (IP tunnelling, as in DsmrHub)
    "Port": 3671,
    "IndividualAddress": "1.1.100"
  },
  "TuyaOptions": {
    "PollIntervalSeconds": 10,        // status poll to cover changes made via the RF remote
    "Devices": [
      {
        "Name": "LivingRoomFan",      // stable key; ties a device to its KNX mapping
        "Enabled": true,
        "IpAddress": "192.168.0.50",
        "DeviceId": "xxxxxxxxxxxxxxxxxxxx",
        "LocalKey": "xxxxxxxxxxxxxxxx",
        "ProtocolVersion": "3.3"
      }
    ]
  },
  "DeviceMappings": {
    "LivingRoomFan": {                // key == TuyaOptions.Devices[].Name
      "FanPowerCommand": "1/1/1",     "FanPowerStatus": "1/1/2",
      "FanSpeedStep":    "1/1/3",     "FanSpeedStatus": "1/1/4",
      "FanDirectionCommand": "1/1/5", "FanDirectionStatus": "1/1/6",
      "FanTimerCommand": "1/1/7",     "FanTimerStatus": "1/1/8",
      "LightPowerCommand": "1/1/9",   "LightPowerStatus": "1/1/10",
      "LightBrightnessCommand": "1/1/11", "LightBrightnessStatus": "1/1/12",
      "LightCctCommand": "1/1/13",    "LightCctStatus": "1/1/14"   // in scope; leave "" to disable per device
    }
  }
}
```

Rules (as in DsmrHub's `KnxMeterReadingHandler`):
- An **empty GA string disables** that function (mapping entry ignored).
- On startup the bridge builds two lookups per device: capability→status telegram, and
  command-GA→(device, capability) so inbound `GroupValueWrite` telegrams are routed to the right
  device/DP, and `GroupValueRead` requests are answered from the last known value.

## 8. Functional requirements

**KNX → Tuya (command path)**
- **FR-1** On a `GroupValueWrite` to a mapped command GA, translate to the corresponding DP write
  and send `CONTROL 0x07` to the device. Speed (DP 62) **must** be sent as an integer.
- **FR-2** Fan speed dim telegrams (3.007) increment/decrement the level within `1..6`, applying
  the dim-up-from-off / dim-down-at-1 rules in §6.
- **FR-3** Direction, timer, light power, and brightness map per the table in §6 (brightness scales
  KNX 0–100 % ↔ DP 0–1000).
- **FR-4** CCT (DP 23) exposes only the 3 discrete steps (0/500/1000). In MVP scope; per-device it
  can still be disabled by leaving its GA empty. Mitigate the known flicker by writing DP 23 only on
  an actual step change.

**Tuya → KNX (feedback path)**
- **FR-5** On a pushed `dps` status update from a device, write the changed values to the mapped
  **status** GAs (byte-reversed `GroupValue`, as DsmrHub does).
- **FR-6** Poll each device every `PollIntervalSeconds` (`DP_QUERY 0x0a`) to catch changes made via
  the physical RF remote, which do not reliably push updates.
- **FR-7** Answer KNX `GroupValueRead` on status GAs with the last known value
  (`RespondGroupValueAsync`), matching DsmrHub's read-response behavior.
- **FR-8** Suppress redundant writes: only send to KNX when a value actually changed (DsmrHub's
  `SequenceEqual` guard).

**Connectivity / lifecycle**
- **FR-9** Maintain one persistent TCP socket per device; send heartbeats; reconnect with backoff on
  drop (see [wind-calm UC-09](use-cases/wind-calm/UC-09-device-offline-recovery.md)).
- **FR-10** KNX and per-device Tuya connections are independent; a single device failing must not
  take down the bridge or other devices.
- **FR-11** `KnxOptions.Enabled = false` disables the KNX bus entirely (as in DsmrHub).

## 9. Non-functional requirements

- **NFR-1 Multi-device:** support N Wind Calm devices from config (target: at least 8).
- **NFR-2 Latency:** KNX command → device action < ~500 ms under normal LAN conditions.
- **NFR-3 Resilience:** survive device power-cycles, gateway restarts, and transient LAN loss
  without manual intervention.
- **NFR-4 Consistency:** reuse Knx.Falcon.Sdk and the DsmrHub options/encoding conventions.
- **NFR-5 Config-only operation:** no rebuild required to add/remap a device.
- **NFR-6 Platform:** runs as a long-lived worker on the target host (same deployment shape as
  DsmrHub).

## 10. Architecture (Domain-Driven Design, layered like DsmrHub)

`tuya-hub` follows **Domain-Driven Design**. The four projects map onto DDD's layered architecture
with the dependency rule pointing inward (Domain depends on nothing; Application depends on Domain;
Infrastructure and Host depend inward). This keeps the DsmrHub project shape while making the **domain
model — not the wire protocols — the center** of the design.

**Strategic framing.** The domain is *bridging a Wind Calm device to KNX*. Both KNX (Knx.Falcon.Sdk)
and Tuya (TuyaNet, §10a) are **external models with their own language**, so each sits behind an
**Anti-Corruption Layer (ACL)**: the domain never sees a `dps` dict or a `GroupValue` byte array — it
speaks only domain terms (endpoints, speed levels, brightness, direction, timer). The two ACLs are the
*only* places those foreign models are translated.

**Ubiquitous language:** Device, Fan / Light *endpoint*, Datapoint (DP), Speed level (1–6), Brightness,
Colour temperature, Direction, Timer, Group Address (GA), Command vs. Status, Mapping.

### Tactical model

- **TuyaHub.Domain** — the model; pure C#, no framework/protocol dependencies.
  - **Aggregate root `Device`** — one Wind Calm unit and the **consistency boundary**. Holds two
    entities, **`FanEndpoint`** and **`LightEndpoint`**; all state transitions and invariants live
    here, not in the Application layer.
  - **Value objects** — `DeviceId`, `LocalKey`, `ProtocolVersion`, `GroupAddress`, `SpeedLevel` (1–6),
    `Brightness`, `ColourTemperature` (3 steps), `Direction`, `Timer` (0–540 min). Self-validating,
    immutable.
  - **Invariants inside the aggregate** (formerly framed as "Application orchestration"): the dim-step
    rules of §6 (dim-up-from-off → power on **and** level 1; dim-down-at-1 → stay), speed clamped to
    1–6, brightness ↔ DP 0–1000 scaling, CCT 3-step semantics, MCU-owned timer.
  - **Domain events** — `DeviceStateChanged`, `FanSpeedChanged`, `DeviceWentOffline`,
    `DeviceReconnected`, etc., raised by the aggregate and dispatched via **MediatR** (as in DsmrHub).
  - **Registry port** — `IDeviceRegistry` abstracts retrieval of the configured aggregates.
- **TuyaHub.Application** — thin **application services / command handlers** that orchestrate use
  cases and **delegate all rules to the aggregate**. Owns the **ports** the ACLs implement
  (`IDeviceGateway` for Tuya, `IKnxBus` for KNX). No business logic of its own.
- **TuyaHub.Infrastructure** — adapters implementing the ports; the two ACLs live here.
  - `Knx/` — **KNX ACL**: `KnxBridge` on **Knx.Falcon.Sdk** (`IpTunnelingConnectorParameters`, `KnxBus`,
    `GroupValueWrite`/`RespondGroupValueAsync`, byte-reversed `GroupValue`, `GroupMessageReceived`).
    Translates telegrams ↔ domain commands/events. Directly analogous to `KnxMeterReadingHandler`.
  - `Tuya/` — **Tuya ACL**: local-protocol adapter over **TuyaNet** (§10a) plus the built
    heartbeat/reconnect/pushed-STATUS reliability layer. Translates `dps` ↔ domain datapoints.
  - `Options/` — `KnxOptions`, `TuyaOptions`, `DeviceMappingOptions`, and the config-backed
    `IDeviceRegistry` implementation.
- **TuyaHub** (host) — generic host / worker; **composition root** wiring via
  `AddInfrastructure(configuration)`, exactly like DsmrHub's `Program.cs` / `DependencyInjection`.

Data flow (the ACLs are the translation points; the aggregate holds the rules):
```
KNX bus ──GroupValueWrite──▶ [KNX ACL] ──▶ Application ──▶ Device aggregate ──[Tuya ACL]──CONTROL──▶ device
KNX bus ◀─Write/Respond── [KNX ACL] ◀── domain event ◀── Device aggregate ◀──[Tuya ACL]──dps push/poll── device
```

## 10a. Tuya local-client decision (resolved)

**Decision: buy the codec, build the reliability layer.** Adopt **`TuyaNet` by ClusterM**
(NuGet `TuyaNet`) for the protocol-3.3 codec/transport, and build a thin connection-supervisor
around it. Do **not** implement 3.3 framing from scratch.

Rationale (from the Tuya-client research):

- **TuyaNet already implements the fiddly, easy-to-get-wrong parts correctly in C#:** `55AA`
  framing, CRC32, AES-128-ECB with the raw 16-char `local_key`, the "`3.3` header on CONTROL but
  **not** on DP_QUERY/heartbeat" rule, and correct **integer** DP serialization (DP 62 as JSON
  number `3`, not `"3"`). It exposes `GetDpsAsync` (DP_QUERY 0x0a), `SetDpsAsync` (CONTROL 0x07), a
  `PermanentConnection` mode, and low-level `EncodeRequest`/`DecodeResponse`.
- **It is the only viable option.** Of the C# packages, only TuyaNet supports the **local** protocol
  at **3.3**. `TuyaKit` is a stale 3.1-only experiment; `kwolo.tuya.net` and `Tuya.Net` are **cloud**
  clients (irrelevant to LAN control).

**Gaps we must build on top of TuyaNet** (needed for a reliable 8-device bridge regardless of
library choice, so no library removes this work):

- **Heartbeat (0x09):** TuyaNet has none. Send a periodic HEART_BEAT frame (or cheap DP_QUERY) or
  the module drops the idle socket (~30 s). Satisfies the persistent-socket requirement in
  [wind-calm UC-09](use-cases/wind-calm/UC-09-device-offline-recovery.md).
- **Background reconnect with backoff:** TuyaNet only retries per-call. Wrap `TuyaDevice`
  (`PermanentConnection = true`) in a supervised connection loop per device (FR-9/FR-10).
- **Pushed-status read loop:** TuyaNet is **poll-only**; it does not surface unsolicited STATUS
  (0x08) frames. Add a background reader that decodes pushed `dps` via `DecodeResponse` for prompt
  KNX mirroring (FR-5), with the interval poll (FR-6) as the backstop for RF-remote changes.

**Constraints noted:**

- **License:** TuyaNet is **GPL-3.0**. Fine for an internal bridge running on the HA host. If tuya-hub
  is ever **redistributed as binaries**, keep the Tuya layer as a separable GPL component or fall back
  to a self-written ~300-line 3.3 codec (tinytuya `PROTOCOL.md` is the reference). Track this before
  any public release.
- **Fork vs reference:** because heartbeat + push decoding aren't cleanly exposed, **vendoring/forking**
  `TuyaParser`/`TuyaDevice` is likely cleaner than fighting the public API. Confirm during M2.
- **Dependency:** TuyaNet targets .NET Standard 2.0 (consumable from net10) and pulls in
  **Newtonsoft.Json** — acceptable, but the one external transitive to be aware of.
- **No 3.4/3.5 headroom:** TuyaNet is 3.3-max. Fine for Wind Calm (`fsd`, 3.3). A future 3.4/3.5
  device would require implementing the session-key handshake then — out of MVP scope.

## 11. Milestones

1. **M1 — Skeleton + config binding.** Solution scaffold (4 projects), `TuyaOptions` /
   `KnxOptions` / `DeviceMappingOptions` bound from `appsettings.json`, DI wiring. No device I/O.
2. **M2 — Tuya local client.** Integrate/vendor **TuyaNet** (§10a); add heartbeat, background
   reconnect, and a pushed-STATUS read loop. Persistent 3.3 socket to one device: query all DPs,
   write a DP, receive pushed `dps`. Verify integer DP 62 and header rules against a real Wind Calm
   (capture one known-good tinytuya exchange to confirm firmware quirks).
3. **M3 — KNX outbound (feedback).** Push + poll device state to status GAs; answer read requests.
4. **M4 — KNX inbound (commands).** Fan power/speed/direction/timer, light power/brightness from
   KNX; dim-step state machine.
5. **M5 — Multi-device + resilience.** N devices from config, per-device isolation, backoff.
6. **M6 — Light CCT** (3-step, flicker-mitigated) and hardening.

## 12. Acceptance criteria (MVP done)

- Two Wind Calm devices, declared only in `appsettings.json`, are each independently controllable
  from KNX for: fan on/off, fan speed up/down (1–6), fan direction, fan timer, light on/off, light
  brightness, and light CCT (3-step).
- Status GAs reflect device state within one poll interval, including changes made via the RF
  remote; KNX read requests are answered.
- No LAN discovery and no cloud calls occur.
- Killing/restarting one device recovers automatically without affecting the other.

## 13. Risks & open questions

- **R1 — .NET Tuya local client. RESOLVED (see §10a):** adopt `TuyaNet` for the 3.3 codec, build
  heartbeat/reconnect/push on top. Residual risk is only the reliability layer + GPL-3.0 licensing
  on any future redistribution, both tracked in §10a.
- **R2 — Speed feedback semantics.** 3.007 has no feedback; confirm the 5.010 status GA + dim rules
  match the operator's expectation on the visualisation.
- **R3 — RF-remote drift** relies on polling; `PollIntervalSeconds` trades bus traffic vs freshness.
- **R4 — Connection ceiling (~3 sockets).** With many devices, ensure only the single persistent
  socket per device is used and the Tuya app is not connected concurrently.
- **Q1 — RESOLVED:** REST/WebSocket is **out of MVP**, but the Application layer is shaped so it can
  be added later without rework. No HTTP surface built now.
- **Q2 — RESOLVED:** KNX uses **IP tunnelling** (`IpTunnelingConnectorParameters`, port 3671), same
  as DsmrHub.
- **Q3 — RESOLVED:** **CCT is in scope** (3-step, flicker-mitigated per FR-4); still disableable
  per device via an empty GA.
