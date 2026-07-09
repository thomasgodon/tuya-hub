# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status: specification only

There is **no application code yet**. This repo currently contains only planning/specification
documents (a PRD and use cases) for a not-yet-built C# app. The `.gitignore` is the standard
Visual Studio one, in place for the future solution. When implementing, follow the PRD's declared
architecture and milestones rather than inventing your own.

**Read first, always:** `docs/PRD-MVP.md` is the source of truth for what to build. It resolves
every major design decision (Tuya client library, KNX transport, CCT scope, REST/WS scope) — do not
re-open those unless asked.

## What tuya-hub is

A C# (**.NET 10**) console/worker app that bridges **CREATE / IKOHS "Windcalm"** ceiling-fan-with-light
devices to a **KNX** bus. Each Tuya device is controlled **locally over the LAN** (Tuya local
protocol **3.3**, per-device local key, **no cloud**) and exposed as KNX group objects for bidirectional
control + status feedback.

The MVP is deliberately narrow: statically-configured devices only (**no LAN discovery**, no cloud,
no REST/WebSocket — those are documented in `docs/use-cases/` as future/secondary surface but are
explicitly out of MVP scope).

## Architecture (Domain-Driven Design — see PRD §10)

**DDD**, four projects mapping onto DDD's layered architecture (dependencies point inward:
Domain ← Application ← Infrastructure/Host). Reuse DsmrHub's cross-cutting patterns — options binding,
byte encoding/endianness, KNX read-response behavior, MediatR — but the **domain model, not the wire
protocols, is the center**.

**The strategic key: both KNX and Tuya are external models behind Anti-Corruption Layers.** The domain
never sees a `dps` dict or a `GroupValue` byte array; each ACL is the only place its foreign model is
translated. When implementing, keep protocol types out of `Domain`/`Application`.

- **TuyaHub.Domain** — pure model, no framework/protocol deps.
  - **Aggregate root `Device`** (one Wind Calm unit; the consistency boundary) with entities
    **`FanEndpoint`** / **`LightEndpoint`**. **All invariants and state transitions live here**, not in
    Application — the dim-step rules, speed clamp 1–6, brightness↔0–1000 scaling, CCT steps, MCU timer.
  - **Value objects**: `DeviceId`, `LocalKey`, `ProtocolVersion`, `GroupAddress`, `SpeedLevel`,
    `Brightness`, `ColourTemperature`, `Direction`, `Timer` — self-validating, immutable.
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
- **TuyaHub** (host) — generic worker; composition root, DI via `AddInfrastructure(configuration)`.

**Tuya local client decision (resolved, PRD §10a):** inside the Tuya ACL, adopt **`TuyaNet` by ClusterM**
(NuGet) for the 3.3 codec/framing/AES — do **not** hand-roll protocol 3.3. Build a thin reliability layer
on top that TuyaNet lacks: **heartbeat (0x09)**, **background reconnect with backoff**, and a
**pushed-STATUS read loop** (TuyaNet is poll-only). Note TuyaNet is **GPL-3.0** and pulls in Newtonsoft.Json.

## Configuration model (`appsettings.json`)

Three bound options sections (PRD §7): `KnxOptions`, `TuyaOptions` (a list of devices — IP, deviceId,
localKey, protocolVersion), and `DeviceMappings` (per-device, keyed by `TuyaOptions.Devices[].Name`).
Group addresses are held as **strings**; an **empty GA string disables that function**. Command and
status GAs are always separate. No rebuild required to add/remap a device.

## Domain constraints that will bite you (from the Wind Calm use cases)

These are firmware/protocol quirks the code must respect — see `docs/use-cases/wind-calm/README.md`:

- **DP 62 (fan speed) must be sent as an integer `1..6`, never a string** — the #1 cause of silent
  speed failures.
- **Fan power (DP 60) and speed (DP 62) are independent DPs.** Adopted dim rules: dim-up from off →
  turn fan on **and** set level 1; dim-down at level 1 → stay at 1 (off only via the power object).
- Fan speed uses KNX **DPT 3.007** (relative dim, no feedback) for command and a **separate DPT 5.010**
  status GA (1–6; 0 = off) for feedback.
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
