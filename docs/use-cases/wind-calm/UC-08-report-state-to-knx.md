# UC-08 — Report device state to KNX (push + poll)

**Summary:** The bridge keeps the KNX status GAs in sync with the real device state, combining
**pushed** `dps` updates with **periodic polling** to catch changes made from the physical RF remote.
**Primary actor:** tuya-hub.
**Stakeholders & interests:** Occupant/visualisation (wants the bus to reflect reality, including
remote-driven changes); integrator (wants bounded, non-flooding feedback traffic).
**Preconditions:**
- Device online with a persistent socket; all mapped status GAs defined (see profile README).
- A poll interval configured (e.g. every 10–30 s) and a heartbeat running.
**Trigger:** Either a pushed `dps` update from the device, or the poll timer elapsing.

## Main flow
1. **Push path:** the device sends an unsolicited `dps` update over the socket.
2. **Poll path:** on the interval, the bridge sends `DP_QUERY 0x0a` and receives the full `dps` set.
3. For each changed DP (60/62/63/64/20/22/23), the bridge maps it to the corresponding KNX value and
   writes the **status GA** only if the value actually changed (change-of-value / suppress duplicates).
4. Fan speed (DP 62) is published on the 5.001 % status GA (level 1–6 → `round(level × 100 / 6)`);
   a fan-off (DP 60 = false) publishes `0 %`.
5. CCT is scaled to % / step before publishing.

## Alternate flows
- **08a — Remote-driven change:** occupant uses the RF remote; no push arrives, but the next poll
  detects the new `dps` and updates the affected status GAs.
- **08b — GroupValueRead from KNX:** if a device on the bus reads a status GA, the bridge answers
  from its last-known cached value (no live device round-trip required).
- **08c — Startup sync:** on connect, the bridge does one full `DP_QUERY` and publishes all status
  GAs to initialise the bus.

## Error scenarios
- **Poll times out:** count consecutive failures; after the threshold, hand off to UC-09 (offline).
- **Feedback storm:** rapid DP changes (e.g. dimming) are rate-limited / debounced so the bus is not
  flooded; only the settled value is published.
- **Stale cache after long disconnect:** on reconnect, force a full re-publish (08c) rather than
  trusting the pre-disconnect cache.

## Postconditions
- Every mapped status GA reflects the latest confirmed device value; remote-driven changes appear on
  the bus within one poll interval.

## Open questions
- Optimal poll interval vs. bus traffic — 10 s (responsive) vs. 30 s (quiet)?
- Should DP 64 (timer remaining) be polled at a finer cadence while a countdown is active?
