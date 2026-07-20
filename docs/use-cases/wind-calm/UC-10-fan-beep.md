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
- **10c — Startup reconciliation (default silence):** DP 66 is a *persistent* buzzer-enable flag and the
  firmware ships it **on**, so the module beeps to acknowledge every LAN `CONTROL` (the RF remote does
  not). The hub therefore enforces a configured desired value once on each (re)connect: after the
  post-connect `DP_QUERY` it compares the reported DP 66 to `TuyaOptions.Devices[].DesiredBeep`
  (default `false`) and issues a single corrective write **only if they differ** (query-then-correct —
  so no beep fires on connect and no redundant write is sent). This is one-shot per connect: a live KNX
  fan-beep change (main flow) is honored and **not** reverted until the next reconnect. Set
  `DesiredBeep: true` to keep the confirmation beep.

## Error scenarios
- **Write not acknowledged / timeout:** retry once; if still failing, mark device offline (UC-09) and
  leave the status GA at the last known value (do not echo an unconfirmed state).
- **Per-device disable:** an empty/absent GA in `DeviceMappings` disables the beep command and/or
  status for that device.

## Postconditions
- DP 66 reflects the commanded value and the fan-beep status GA matches the device.

## Open questions
- None.
