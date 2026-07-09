# UC-04 — Fan countdown timer from KNX

**Summary:** A KNX value telegram sets an auto-off countdown (minutes) via Tuya DP 64; the device
MCU counts down and switches the fan off. The bridge reports remaining minutes back to KNX.
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (run the fan for N minutes then stop); integrator (wants the
countdown to be the device's job, not the bridge's).
**Preconditions:**
- Device online; DP 64 mapped to a command GA (DPT 7.006, minutes) and a status GA (remaining
  minutes). Valid range **0–540** (up to 9 h).
**Trigger:** A `GroupValueWrite` arrives on the timer command GA.

## Main flow
1. Bridge receives a 7.006 value (minutes) and clamps it to `[0, 540]`.
2. Bridge writes DP 64 with the integer minute count via `CONTROL 0x07`.
3. The **device MCU** owns the countdown from here — it decrements and turns the fan off at zero.
4. Bridge periodically reads DP 64 (see UC-08 poll) and publishes remaining minutes on the status GA.
5. When the timer expires, the device sets DP 60 = false; the bridge reflects that on the fan-power
   status GA (UC-01) and the timer status GA reads `0`.

## Alternate flows
- **04a — Cancel timer:** command `0` writes DP 64 = 0; the fan keeps running (only the timer is
  cleared).
- **04b — Timer set while fan off:** device behaviour varies (may start the fan or just arm the
  timer). Bridge writes the value and relies on readback; it does not force power state.
- **04c — Re-arm:** a new value while a countdown is active replaces the remaining time.

## Error scenarios
- **Bridge restart mid-countdown:** the MCU keeps counting; on reconnect the bridge reads DP 64 and
  resumes reporting the true remaining value (it must **not** re-implement or reset the timer).
- **Write not acknowledged:** retry once; keep timer status GA at last confirmed value.

## Postconditions
- DP 64 holds the intended remaining minutes; timer status GA matches; power follows the MCU at
  expiry.

## Open questions
- Is the unit minutes on this firmware, or a step index? Confirm against the live device.
- Should the status GA update be event-driven or only on the UC-08 poll interval (affects how
  precisely "remaining minutes" tracks on the bus)?
