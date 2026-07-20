# UC-10 — Fan confirmation beep from KNX

**Summary:** A KNX switch telegram enables or disables the fan's confirmation beep via Tuya DP 66
(`fan_beep`); the current setting is reported back to KNX.
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (wants to silence/enable the audible confirmation); integrator
(wants reliable command + feedback, consistent with the other boolean functions).
**Preconditions:**
- Device discovered, `local_key` configured, protocol set, persistent local socket up.
- KNX gateway connected; command GA (DPT 1.001) and status GA (DPT 1.001) mapped to DP 66.

**Trigger:** A `GroupValueWrite` arrives on the fan-beep command GA.

## Main flow
1. Bridge receives a 1.001 telegram: `1` = beep on, `0` = beep off.
2. Bridge issues `CONTROL 0x07` writing DP 66 = `true`/`false`.
3. Device applies and returns/acknowledges the new `dps` state.
4. Bridge reads back DP 66 and publishes it on the fan-beep **status GA**.

## Alternate flows
- **10a — Independent of fan power:** the beep setting is applied whether the fan is on or off; it does
  not turn the fan on or off.
- **10b — Idempotent command:** command equals current state → re-publish status; no write strictly
  required.

## Error scenarios
- **Write not acknowledged / timeout:** retry once; if still failing, mark device offline (UC-09) and
  leave the status GA at the last known value (do not echo an unconfirmed state).
- **Per-device disable:** an empty/absent GA in `DeviceMappings` disables the beep command and/or
  status for that device.

## Postconditions
- DP 66 reflects the commanded value and the fan-beep status GA matches the device.

## Open questions
- None.
