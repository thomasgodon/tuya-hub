# Use Cases — tuya-hub

`tuya-hub` is a C# console application that bridges [Tuya](https://www.tuya.com/) smart-home
devices to a **KNX** building-automation bus. Its **primary purpose** is to expose Tuya devices
as KNX group objects so they can be controlled from — and report their state back to — a KNX
installation. It also exposes a secondary **REST/WebSocket API** for other clients.

> **MVP scope:** see [PRD-MVP.md](../PRD-MVP.md) — a KNX bridge for statically-configured Wind Calm
> devices (no LAN discovery, no cloud, no REST/WebSocket).

Tuya devices are controlled **locally over the LAN** (Tuya local protocol, using each device's
local key) — there is no dependency on the Tuya Cloud for day-to-day operation.

## Index

| ID | Use case | Primary actor |
|----|----------|---------------|
| [UC-01](UC-01-discover-tuya-devices.md) | Discover Tuya devices on the LAN | Operator |
| [UC-02](UC-02-configure-device-mapping.md) | Configure a device ↔ KNX mapping | Operator |
| [UC-03](UC-03-connect-knx-gateway.md) | Connect to the KNXnet/IP gateway | tuya-hub |
| [UC-04](UC-04-control-tuya-from-knx.md) | Control a Tuya device from KNX | KNX installation |
| [UC-05](UC-05-report-tuya-state-to-knx.md) | Report Tuya device state to KNX | tuya-hub |
| [UC-06](UC-06-control-device-via-rest.md) | Control a device via the REST API | API client |
| [UC-07](UC-07-query-state-via-rest.md) | Query device state via the REST API | API client |
| [UC-08](UC-08-subscribe-state-via-websocket.md) | Subscribe to state changes via WebSocket | API client |
| [UC-09](UC-09-handle-device-offline-recovery.md) | Handle device offline / recovery | tuya-hub |

## Device-scoped use cases

Each supported device type is a **profile** (its Tuya datapoints ↔ domain ↔ KNX group objects);
adding a device type means registering a profile, not changing the shared bridge. Detailed,
per-capability use cases for a profile's device live in subfolders:

| Device profile | Use cases |
|--------|-----------|
| CREATE / IKOHS **Windcalm** ceiling fan + light (`fsd`, protocol 3.3) — profile #1 | [wind-calm/](wind-calm/README.md) |

## Use case template

Each file follows this structure:

```
# UC-NN — <Title>

**Summary:** one or two sentences.
**Primary actor:** who initiates.
**Stakeholders & interests:** who cares and why.
**Preconditions:** what must be true before.
**Trigger:** what starts the flow.

## Main flow
1. ...

## Alternate flows
- **NNa:** ...

## Error scenarios
- ...

## Postconditions
- ...

## Open questions
- ...
```

## Glossary

| Term | Meaning |
|------|---------|
| **DP (datapoint)** | A Tuya device capability identified by a numeric DP ID, with a type (`bool`, `value`, `enum`, `string`, `raw`). |
| **Local key** | Per-device secret required to talk to a Tuya device locally. Obtained once via the Tuya IoT platform. |
| **Tuya protocol version** | Wire protocol version of the device (e.g. `3.3`, `3.4`, `3.5`); affects framing/encryption. |
| **GA (group address)** | A KNX address (e.g. `1/2/3`) that carries a value on the bus. |
| **DPT (datapoint type)** | KNX value encoding, e.g. `1.001` (switch), `5.001` (0–100 %), `9.x` (2-byte float). |
| **KNXnet/IP** | IP transport for KNX. **Tunnelling** = point-to-point via a gateway; **routing** = multicast. |
| **Mapping** | The declared link between one Tuya DP and one or more KNX group addresses (command + feedback). |
