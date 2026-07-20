# UC-07 — Light colour temperature from KNX *(optional)*

**Summary:** A KNX telegram selects one of the LED's discrete colour-temperature steps via Tuya
DP 23 (`temp_value`, values 0 / 500 / 1000). **Optional** — the DP has a known firmware flicker bug.
**Primary actor:** KNX installation.
**Stakeholders & interests:** Occupant (cool vs. warm white); integrator (aware of the flicker
trade-off and may choose to disable this UC).
**Preconditions:**
- Device online; DP 23 mapped. Because the device exposes only **three** steps, KNX carries them as
  a 5.001 % band or an explicit 3-value enum:
  - `0` = cool/white, `500` = warm-white, `1000` = warm.
**Trigger:** A `GroupValueWrite` arrives on the CCT command GA (absolute `LightCctCommand`) **or** the
CCT step GA (relative `LightCctStep`).

The device exposes CCT to KNX **two ways**, independently configurable (either, both, or neither):
- **Absolute — `LightCctCommand` (DPT 5.001 %)**: a percentage snapped to the nearest step. Good for
  scenes / "jump to this temperature".
- **Relative — `LightCctStep` (DPT 3.007 dim step)**: a long-press cycles the steps
  cool → warm-white → warm → cool …, **wrapping** at the rails (1000 up → 0, 0 down → 1000). Good for a
  single pushbutton. Both feed back on the shared `LightCctStatus`.

## Main flow
1. Bridge receives the CCT command:
   - absolute → maps the % to the nearest of the three discrete DP values;
   - step → moves ±1 index into {0, 500, 1000} with wrap-around (break/stop telegrams ignored).
2. Bridge writes DP 23 via `CONTROL 0x07`.
3. Bridge reads back DP 23 and publishes the mapped step on the status GA.

## Alternate flows
- **07a — Feature disabled:** integrator omits the DP-23 mappings entirely; the UC does not apply and
  the light is treated as on/off-only (UC-05). This is the recommended default given the bug.
- **07b — Continuous KNX input:** if a stepless % arrives on the absolute GA, snap to the nearest of
  {0, 500, 1000}.
- **07c — Step GA only:** a pushbutton mapped to `LightCctStep` cycles the three steps with no need for
  the absolute GA; the step relative to the last device-reported step (before any report it starts from
  the coolest, 0).

## Error scenarios
- **Flicker / step-advance bug:** writing DP 23 may cycle to the *next* step and briefly flicker the
  light off/on. The bridge validates via readback and publishes the **actual** resulting step, not
  the requested one.
- **Write not acknowledged:** retry once; keep status GA at last confirmed step.

## Postconditions
- DP 23 holds one of {0, 500, 1000}; CCT status GA reflects the actual device step, regardless of
  whether the change came from the absolute or the step command GA.

## Open questions
- Is the flicker acceptable for the installation, or should this UC ship disabled by default?
- Confirm the exact set of supported values on the live firmware (some units expose more steps).
