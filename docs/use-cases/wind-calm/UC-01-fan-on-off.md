# UC-01 — Fan on/off from KNX

**Summary:** A KNX switch telegram turns the ceiling fan on or off via Tuya DP 60 (`fan_switch`).
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (wants the fan to respond immediately); integrator (wants
reliable command + feedback).
**Preconditions:**
- Device discovered, `local_key` configured, protocol `3.3` set, persistent local socket up.
- KNX gateway connected; command GA (DPT 1.001) and status GA mapped to DP 60.
**Trigger:** A `GroupValueWrite` arrives on the fan-power command GA.

## Main flow
1. Bridge receives a 1.001 telegram: `1` = on, `0` = off.
2. Bridge issues `CONTROL 0x07` writing DP 60 = `true`/`false`.
3. Device applies and returns/acknowledges the new `dps` state.
4. Bridge reads back DP 60 and publishes it on the fan-power **status GA**.

## Alternate flows
- **01a — On implies a running speed:** when turning on, if DP 62 is unset/0, the bridge lets the
  device restore its last level (device behaviour); no speed write is forced here. (Speed-driven
  power-on is handled in UC-02.)
- **01b — Idempotent command:** command equals current state → still confirm by re-publishing
  status; no write strictly required.

## Error scenarios
- **Write not acknowledged / timeout:** retry once; if still failing, mark device offline (UC-09)
  and leave status GA at last known value (do not echo an unconfirmed state).
- **Socket refused (connection limit):** defer to UC-09 recovery; command is dropped with a logged
  warning.

## Postconditions
- DP 60 reflects the commanded value and the fan-power status GA matches the device.

## Open questions
- Should power-off also cancel a running countdown (DP 64), or leave it for the MCU to expire?
