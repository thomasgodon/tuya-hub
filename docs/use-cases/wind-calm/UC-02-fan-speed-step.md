# UC-02 — Fan speed from KNX

> **⚠️ CHANGED — fan speed is now an absolute percentage, not a relative step.** This UC originally
> specified a relative **DPT 3.007** dim-step command (±1 level) with a raw **DPT 5.010** counter
> status. A KNX visualization drives speed as a **percentage**, and the relative step could never set
> an absolute value (and a 5.010 level byte read back as ~1 % when a client typed the object as 5.001).
> The command is now an **absolute DPT 5.001 %** value and the status is reported as **DPT 5.001 %**
> too; the relative 3.007 step was removed. The historical relative-step flow is retained below the
> line for context.

**Summary:** A KNX percentage telegram (DPT 5.001) sets the fan speed absolutely; the bridge maps the
`%` onto the device level `1..6` on DP 62 and reports the current level back as a `%` on a separate
5.001 status GA. `0 %` turns the fan off.
**Primary actor:** KNX installation (a % slider / dimmer object, or a scene).
**Stakeholders & interests:** Occupant (direct speed control from the wall/visualization); integrator
(wants a predictable absolute mapping).
**Preconditions:**
- Device online; DP 62 mapped to a **command GA (DPT 5.001 %)** and a **status GA (DPT 5.001 %)**.
- DP 60 (power) mapped per UC-01.
**Trigger:** A `GroupValueWrite` arrives on the fan-speed command GA (`FanSpeed`) carrying a 5.001 %.

## Main flow
1. Bridge decodes the 5.001 telegram to a percentage `0..100`.
2. `0 %` → the fan is switched **off** (DP 60 = false); flow ends (see 02a).
3. `1..100 %` → level = `ceil(% / 100 × 6)`, clamped to `[1, 6]`
   (1–16→1, 17–33→2, 34–50→3, 51–66→4, 67–83→5, 84–100→6).
4. Bridge writes DP 62 as an **integer** (never a string) via `CONTROL 0x07`; if the fan was off it
   also writes DP 60 = true (see 02b).
5. Bridge reads back DP 62 and publishes the level on the speed **status GA** as a `%`:
   `round(level × 100 / 6)` (1→17, 2→33, 3→50, 4→67, 5→83, 6→100), or `0 %` when off.

## Alternate flows
- **02a — 0 % while running:** DP 60 = false (fan off); the last level is left untouched. Already off →
  nothing sent.
- **02b — Non-zero % while off:** the bridge sets DP 60 = true **and** DP 62 = the mapped level (the fan
  turns on at that speed). Both status GAs update.
- **02c — Same level as current:** the mapped level already matches the running level → no DP write
  (the status GA already reflects it).

## Error scenarios
- **Wrong JSON type:** guard against emitting DP 62 as a string — the device silently ignores it and
  speed never changes. Value is serialised as an int.
- **Write not acknowledged:** retry once; on failure keep the status GA at the last confirmed level.
- **Readback differs from commanded level:** trust the device readback and publish that (device is
  authoritative). Because there are only six discrete levels, the status `%` snaps to the nearest
  band representative, so a slider set to e.g. 60 % reads back as 67 % (level 4).

## Postconditions
- DP 62 holds an integer in `1..6`; the speed status GA (5.001 %) reflects it; power state consistent
  with alternate flows 02a/02b.

---

## Historical — relative 3.007 dim-step (removed)

The original design: a KNX 4-bit dim telegram (DPT 3.007) stepped the fan `±1` level (step-width and
the stop/break code ignored), with the level reported on a separate **DPT 5.010** counter status GA
(`0` = off, `1..6` = running). Dim-up from off turned the fan on at level 1; dim-down at level 1 stayed
at 1 (power off only via UC-01). This was replaced by the absolute-% command above; the
`FanSpeedStep` mapping key was renamed `FanSpeed`.
