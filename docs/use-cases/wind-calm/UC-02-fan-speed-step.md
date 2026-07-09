# UC-02 — Fan speed step up/down from KNX

**Summary:** A KNX 4-bit dim telegram (DPT 3.007) steps the fan speed up or down; the bridge walks
the device level `1..6` on DP 62 and reports the resulting level on a separate 5.010 status GA.
**Primary actor:** KNX installation (typically a physical rocker/dim button).
**Stakeholders & interests:** Occupant (fine speed control from the wall); integrator (wants a
predictable, bounded step behaviour).
**Preconditions:**
- Device online; DP 62 mapped to a **command GA (DPT 3.007)** and a **status GA (DPT 5.010)**.
- DP 60 (power) mapped per UC-01.
**Trigger:** A `GroupValueWrite` arrives on the fan-speed command GA carrying a 3.007 step.

## Main flow
1. Bridge decodes the 3.007 telegram to a direction: **increase** or **decrease**.
2. Every step is treated as **±1 level** (step-width and the stop/break code are ignored for a
   6-level fan).
3. Bridge computes the new level, clamped to `[1, 6]`.
4. Bridge writes DP 62 as an **integer** (never a string) via `CONTROL 0x07`.
5. Bridge reads back DP 62 and publishes the level (1–6) on the speed **status GA** (5.010).

## Alternate flows
- **02a — Step up while fan is off:** if DP 60 = false, the bridge sets DP 60 = true **and** DP 62 = 1
  (fan turns on at lowest speed). Status GAs for both power and speed are updated.
- **02b — Step down at level 1:** level stays at 1; the fan is **not** switched off (power is only
  controlled via UC-01). Status GA re-published as 1.
- **02c — Step up at level 6 / down already at 1:** clamp; no DP write beyond confirming status.
- **02d — Proportional steps (future):** honour the 3.007 step-width so a larger increment jumps
  multiple levels. Not implemented by default.

## Error scenarios
- **Wrong JSON type:** guard against emitting DP 62 as a string — the device silently ignores it and
  speed never changes. Value is serialised as an int.
- **Write not acknowledged:** retry once; on failure keep the status GA at the last confirmed level.
- **Readback differs from commanded level:** trust the device readback and publish that (device is
  authoritative).

## Postconditions
- DP 62 holds an integer in `1..6`; the speed status GA (5.010) matches it; power state consistent
  with alternate flows 02a/02b.

## Open questions
- Should the 5.010 status encode "off" as `0`, or should off be inferred only from the power status
  GA (UC-01)? Current model: `0` = off on the status GA, `1..6` = running level.
- Do we ever want an absolute-speed command GA (5.001 % or 5.010) alongside the relative one?
