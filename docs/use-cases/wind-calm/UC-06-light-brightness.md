# UC-06 — Light brightness from KNX

**Summary:** A KNX percentage telegram (DPT 5.001) sets the LED brightness via Tuya DP 22
(`bright_value`, 0–1000), with scaling in both directions.
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (dimmable light); integrator (wants correct % ↔ 0–1000
scaling and a sane minimum).
**Preconditions:**
- Device online; DP 22 mapped to a command GA (DPT 5.001 %) and a status GA (5.001 %).
- Scaling defined: `dp = round(pct / 100 * 1000)`, `pct = round(dp / 1000 * 100)`.
**Trigger:** A `GroupValueWrite` arrives on the brightness command GA.

## Main flow
1. Bridge receives a 5.001 value (0–100 %).
2. Bridge scales to the DP range and clamps to `[MIN, 1000]` (MIN protects against a value the
   device treats as off / invalid).
3. Bridge writes DP 22 as an integer via `CONTROL 0x07`.
4. Bridge reads back DP 22, scales to %, and publishes on the brightness status GA.

## Alternate flows
- **06a — 0 %:** decide policy — either write the device minimum brightness, or turn the light off
  via DP 20 (UC-05). Current model: `0 %` → light off (DP 20 = false); `>0 %` ensures DP 20 = true.
- **06b — Brightness set while light off:** bridge sets DP 20 = true then DP 22, so a dim command
  also switches the light on.
- **06c — Rounding drift:** repeated small steps may round to the same DP value; that is expected
  and status reflects the actual device value.

## Error scenarios
- **Write not acknowledged:** retry once; keep status GA at last confirmed value.
- **Device clamps low values:** if the device raises a too-low value to its own minimum, trust the
  readback and publish the effective %.

## Postconditions
- DP 22 holds the scaled brightness; brightness status GA matches (in %); power state consistent
  with 06a/06b.

## Open questions
- What is the device's effective minimum `bright_value` (the point below which the LED is off or
  unstable)? Set `MIN` from a live test.
- Should 0 % mean "off" or "dimmest"? (Model above assumes off.)
