# Use Cases — Wind Calm ceiling fan + light (KNX bridge)

These use cases are **scoped to a single device**: the **CREATE / IKOHS "Windcalm" DC ceiling
fan with integrated LED light**. `tuya-hub` controls it **locally over the LAN** (Tuya local
protocol, no cloud dependency) and exposes it as **KNX group objects** so it can be driven from,
and report state back to, a KNX installation.

The device is **two logical endpoints in one unit**: a **fan** and a **light**. They are
controlled independently.

## Device profile

| Property | Value |
|----------|-------|
| Product | CREATE / IKOHS **Windcalm** DC ceiling fan + LED light |
| Tuya category | `fsd` (ceiling fan light) |
| Local protocol | **3.3** (AES-128-ECB, `55AA` framing, CRC32, no session handshake) |
| Transport | TCP **6668**, single persistent socket, heartbeat (`0x09`) every ~9–15 s |
| Read / write | `DP_QUERY 0x0a` to read all DPs, `CONTROL 0x07` to write |
| Secret | per-device `local_key` (obtained once via Tuya IoT; used **only locally** thereafter) |

### Datapoints (DPS)

| Group | DP | Code | Type | Range / values | Meaning |
|-------|----|------|------|----------------|---------|
| Fan | 60 | `fan_switch` | bool | true/false | Fan on/off |
| Fan | 62 | `fan_speed` | **int** | **1–6** | Fan speed level (**send as integer, never string**) |
| Fan | 63 | `fan_direction` | enum | `forward` / `reverse` | Blade direction (summer / winter) |
| Fan | 64 | `countdown_left_fan` | int | 0–540 (minutes) | Auto-off timer; **counted down by the device MCU** |
| Light | 20 | `switch_led` | bool | true/false | Light on/off |
| Light | 22 | `bright_value` | int | 0–1000 | Brightness — **not exposed; hardware ignores the write** (DP present in firmware but has no effect) |
| Light | 23 | `temp_value` | int | 0 / 500 / 1000 | Colour temperature (**optional — firmware flicker bug**) |

## KNX group-object model

Command (KNX → Tuya) and feedback (Tuya → KNX) are **separate group addresses** for every function.

| Function | DP | Command GA (DPT) | Status GA (DPT) |
|----------|----|------------------|-----------------|
| Fan power | 60 | 1.001 switch | 1.001 |
| Fan speed | 62 | **3.007** dim step (up/down) | **5.010** count (1–6; 0 = off) |
| Fan direction | 63 | 1.001 (0 = forward/summer, 1 = reverse/winter) | 1.001 |
| Fan timer | 64 | 7.006 minutes | 7.006 remaining |
| Light power | 20 | 1.001 switch | 1.001 |
| Light CCT *(optional)* | 23 | 5.001 % → 3 steps | 5.001 % |

**Speed is relative:** the bridge receives 4-bit dim telegrams (DPT 3.007) and walks the level
`1..6`. Because a 3.007 object cannot carry feedback, current speed is reported on a **separate
5.010 status GA**.

## Index

| ID | Use case | Endpoint | Primary actor |
|----|----------|----------|---------------|
| [UC-01](UC-01-fan-on-off.md) | Fan on/off from KNX | Fan | KNX installation |
| [UC-02](UC-02-fan-speed-step.md) | Fan speed step up/down from KNX | Fan | KNX installation |
| [UC-03](UC-03-fan-direction.md) | Fan direction (summer/winter) from KNX | Fan | KNX installation |
| [UC-04](UC-04-fan-timer.md) | Fan countdown timer from KNX | Fan | KNX installation |
| [UC-05](UC-05-light-on-off.md) | Light on/off from KNX | Light | KNX installation |
| [UC-06](UC-06-light-brightness.md) | Light brightness from KNX — **removed (hardware has no dimming)** | Light | — |
| [UC-07](UC-07-light-colour-temperature.md) | Light colour temperature from KNX *(optional)* | Light | KNX installation |
| [UC-08](UC-08-report-state-to-knx.md) | Report device state to KNX (push + poll) | both | tuya-hub |
| [UC-09](UC-09-device-offline-recovery.md) | Device offline / reconnect | both | tuya-hub |

## Cross-cutting notes / quirks

- **Speed type:** DP 62 must be written as an **integer** `1..6`. Sending a string is the #1 cause
  of silent speed failures on local Tuya fans.
- **Power vs. speed are independent DPs.** Rule adopted here: *dim-up from off* → turn fan on
  (DP 60) **and** set level 1; *dim-down at level 1* → stay at 1 (fan is switched off only via the
  power object). See UC-01 / UC-02.
- **Timer is MCU-owned.** The bridge sets/reads DP 64 remaining minutes; it never counts down
  locally (see UC-04).
- **CCT flicker:** writing DP 23 can advance to the next step and flicker the light off/on. UC-07
  is optional and exposes only the 3 discrete steps.
- **State drift:** changes from the physical RF remote do **not** reliably push a `dps` update, so
  feedback relies on periodic polling in addition to pushed status (see UC-08).
- **Connection limit ~3:** the module accepts only a few concurrent TCP sessions on 6668. Keep the
  single persistent socket and do **not** run the Smart Life / Tuya app at the same time (see UC-09).

## Glossary

| Term | Meaning |
|------|---------|
| **DP (datapoint)** | A Tuya device capability identified by a numeric DP ID, with a type. |
| **Local key** | Per-device secret required to talk to the device locally. |
| **`fsd`** | Tuya category "ceiling fan light" — combines fan and light DPs. |
| **GA (group address)** | A KNX address (e.g. `1/2/3`) that carries a value on the bus. |
| **DPT (datapoint type)** | KNX value encoding, e.g. `1.001` switch, `3.007` dim control, `5.001` %, `5.010` count, `7.006` minutes. |
| **Level** | Fan speed as the device sees it: an integer `1..6` (DP 62). |
