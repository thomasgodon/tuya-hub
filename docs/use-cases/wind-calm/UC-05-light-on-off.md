# UC-05 — Light on/off from KNX

**Summary:** A KNX switch telegram turns the integrated LED light on or off via Tuya DP 20
(`switch_led`), independently of the fan.
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (light control from the wall/scene); integrator (wants the
light treated as its own endpoint).
**Preconditions:**
- Device online; DP 20 mapped to a command GA (DPT 1.001) and a status GA.
**Trigger:** A `GroupValueWrite` arrives on the light-power command GA.

## Main flow
1. Bridge receives a 1.001 telegram: `1` = on, `0` = off.
2. Bridge writes DP 20 = `true`/`false` via `CONTROL 0x07`.
3. Bridge reads back DP 20 and publishes it on the light-power status GA.

## Alternate flows
- **05a — On restores prior state:** turning on lets the device restore its last light state; the
  bridge does not force any additional write here. (Brightness/dimming is not supported — see UC-06.)
- **05b — Idempotent command:** re-publish status; no write required.
- **05c — No confirmation beep:** the light command produces a single DP 20 write and injects no beep.
  The module's audible acknowledgement on LAN commands is governed by the persistent DP 66 buzzer flag,
  which the hub silences by default on connect (UC-10 10c) — so, unlike the RF remote path, an on/off
  from KNX is silent once reconciled. Set `DesiredBeep: true` to keep the beep.

## Error scenarios
- **Write not acknowledged:** retry once; keep status GA at last confirmed value on failure.
- **Light flicker after a recent CCT change:** if UC-07 was just used, a flicker may be observed;
  it is a device firmware artefact, not a bridge fault.

## Postconditions
- DP 20 reflects the commanded value; light-power status GA matches.

## Open questions
- Should light and fan share a KNX "device online" status, or expose independent availability?
