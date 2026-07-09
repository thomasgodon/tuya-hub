# UC-04 — Control a Tuya device from KNX

**Summary:** A KNX telegram on a mapped command group address is translated into a Tuya local
command, changing the device's state.
**Primary actor:** KNX installation (a switch, logic block, or visualisation writing to a GA).
**Stakeholders & interests:** End user expects the device to respond promptly; operator expects
correct value translation and no runaway loops.
**Preconditions:** Bus connection healthy (UC-03); a mapping exists whose command GA matches the
telegram (UC-02); the target device is reachable on the LAN.
**Trigger:** A GroupValueWrite telegram arrives on a command group address.

## Main flow
1. tuya-hub receives the group telegram and matches it to a mapping by group address.
2. It decodes the KNX value using the mapping's DPT (e.g. DPT 1.001 → on/off, DPT 5.001 → %).
3. It applies the configured transform/scaling to produce the Tuya DP value (e.g. 0–100 % →
   0–1000).
4. It sends the corresponding `set` command to the device over the Tuya local protocol.
5. The device acknowledges / returns its new state.
6. The resulting state change is reported back to KNX per UC-05 (feedback GA), closing the loop.

## Alternate flows
- **1a. No matching mapping:** telegram is ignored (optionally logged at debug level).
- **4a. Device currently offline:** command is rejected/queued per policy; see UC-09.
- **6a. Loop suppression:** the feedback resulting from a KNX-originated command must not be
  re-interpreted as a new command, and must not oscillate with the source.

## Error scenarios
- Value out of the DP's valid range after transform → clamp or reject per configuration; log it.
- Local command times out or the device NAKs → retry a bounded number of times, then surface an
  error and leave the device state unchanged.
- Read-only DP receives a command → reject and log.

## Postconditions
- On success: device DP reflects the commanded value and the KNX feedback GA has been updated.
- On failure: device state unchanged; the failure is logged and observable via health/API.

## Open questions
- Debounce/rate-limit for rapidly-repeated telegrams (e.g. dimming ramps)?
- Optimistic feedback (echo the command immediately) vs. wait for device confirmation?
