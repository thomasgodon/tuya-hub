# UC-09 — Device offline / reconnect

**Summary:** The bridge detects loss of the local connection to the ceiling fan, reflects
unavailability on KNX, and re-establishes the single persistent socket when the device returns.
**Primary actor:** tuya-hub.
**Stakeholders & interests:** Occupant (wants control to resume automatically); integrator (wants a
clear availability signal and no hammering of the device's limited connection slots).
**Preconditions:**
- Device previously configured and online; heartbeat (`0x09`) and poll (UC-08) running.
- Optional KNX availability object mapped (e.g. 1.001 or a status GA) for the device.
**Trigger:** Heartbeat/poll failures reach the configured threshold, or the socket drops.

## Main flow
1. Bridge detects N consecutive heartbeat/`DP_QUERY` failures or a socket error.
2. Bridge marks the device **offline**: sets the KNX availability object (if mapped) and stops
   echoing commands as confirmed.
3. Bridge enters a reconnect loop with **backoff** (e.g. 1 s → 2 s → 5 s → 30 s cap), never opening
   more than the single socket (respect the ~3-connection module limit).
4. On reconnect, the bridge performs the protocol setup for **3.3** (AES-ECB with `local_key`) and a
   full `DP_QUERY`.
5. Bridge re-publishes all status GAs (UC-08 startup sync) and clears the offline signal.

## Alternate flows
- **09a — Connection refused (slot exhausted):** likely the Tuya app or another client holds a slot.
  Bridge keeps backing off and logs a hint to close the app; it does not open parallel sockets.
- **09b — Commands during outage:** KNX commands received while offline are dropped with a logged
  warning (no queue), to avoid applying stale intent on reconnect. *(Queue-and-replay is a possible
  future option — see open questions.)*
- **09c — IP changed (DHCP):** if reconnect to the known IP fails, trigger re-discovery to find the
  device's new address before resuming.

## Error scenarios
- **local_key rejected after reconnect:** key may have been rotated by a cloud re-pairing; surface a
  clear error — day-to-day operation is local, but a rotated key requires re-provisioning.
- **Protocol version mismatch (3.4/3.5 firmware):** if the handshake fails as 3.3, log that the unit
  may have upgraded; DP map is unchanged but transport/crypto must be adapted.

## Postconditions
- While offline: availability signalled on KNX; last-known status GAs retained (not zeroed).
- After recovery: single socket restored, full state re-published, commands honoured again.

## Open questions
- Should commands issued during an outage be queued and replayed, or always dropped (current model)?
- How aggressively to re-discover on IP change vs. relying on a static DHCP reservation?
