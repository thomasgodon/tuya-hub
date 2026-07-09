# UC-03 — Fan direction (summer/winter) from KNX

**Summary:** A KNX switch telegram sets the blade rotation direction via Tuya DP 63
(`fan_direction`, enum `forward` / `reverse`).
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (summer downdraft vs. winter updraft); integrator (wants a
clear encoding of which bit means which direction).
**Preconditions:**
- Device online; DP 63 mapped to a command GA (DPT 1.001) and a status GA.
- Encoding fixed: **`0` = `forward` (summer)**, **`1` = `reverse` (winter)**.
**Trigger:** A `GroupValueWrite` arrives on the fan-direction command GA.

## Main flow
1. Bridge receives a 1.001 telegram and maps it to the enum: `0`→`forward`, `1`→`reverse`.
2. Bridge writes DP 63 with the exact enum casing the device expects (`"forward"` / `"reverse"`).
3. Device applies; bridge reads back DP 63 and publishes the mapped bit on the status GA.

## Alternate flows
- **03a — Change while fan is off:** direction is still written and reflected; it takes visible
  effect on the next fan-on. No implicit power change.
- **03b — Change while running:** some units briefly ramp down/up to reverse; treat any transient
  as normal and rely on readback for the final state.

## Error scenarios
- **Enum casing mismatch:** the device rejects an unknown enum string silently — the bridge must use
  the device's exact tokens; verified once against the live unit.
- **Write not acknowledged:** retry once; keep status GA at last confirmed direction on failure.

## Postconditions
- DP 63 holds `forward`/`reverse`; direction status GA matches.

## Open questions
- Confirm the enum tokens on the actual firmware (`forward`/`reverse` vs. numeric enum index).
