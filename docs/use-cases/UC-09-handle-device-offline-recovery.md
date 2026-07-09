# UC-09 — Handle device offline / recovery

**Summary:** tuya-hub detects when a Tuya device becomes unreachable, reflects that state
everywhere, and recovers automatically when the device returns.
**Primary actor:** tuya-hub (system, continuously).
**Stakeholders & interests:** End users want the system to self-heal; operator wants clear
visibility into what is down and why; KNX logic needs a trustworthy online/offline signal.
**Preconditions:** At least one device is configured (UC-02).
**Trigger:** A local command/connection fails, a poll times out, or push updates stop arriving.

## Main flow
1. tuya-hub detects a device is unreachable (connection error or repeated timeouts beyond a
   threshold).
2. It marks the device **offline** in the state cache.
3. It reflects offline status: optionally on a dedicated KNX status GA, and to REST/WebSocket
   clients (UC-07/UC-08).
4. It attempts to reconnect on a backoff schedule without blocking other devices.
5. On reconnection, it reads current DP values, marks the device **online**, and publishes fresh
   feedback to KNX (UC-05) and subscribers.

## Alternate flows
- **1a. Transient blip:** a single failure within tolerance does not flip the device offline.
- **4a. Commands while offline:** incoming KNX/REST commands (UC-04/UC-06) are rejected or
  queued per the configured policy; queued commands have a max age.
- **5a. Value changed while offline:** the post-recovery read may differ from the last-known
  value; the fresh value wins and is published as a normal change.

## Error scenarios
- IP changed (DHCP) while offline → re-resolve via discovery (UC-01) before giving up.
- Permanent failure (device removed) → keep reporting offline; surface prominently in health.

## Postconditions
- Device online/offline status is accurate across KNX, REST, and WebSocket, and the bridge has
  either recovered the device or is still retrying without affecting others.

## Open questions
- Offline command policy default: reject vs. queue-with-max-age?
- Should offline devices dim/blank their KNX feedback, or hold the last-known value?
