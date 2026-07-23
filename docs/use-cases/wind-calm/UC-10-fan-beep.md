# UC-10 — Fan confirmation beep from KNX — **REMOVED (nuisance-only; now force-silenced)**

> **This use case has been removed.** The confirmation beep (DP 66 `fan_beep`) was a pure nuisance:
> the device beeps to acknowledge every LAN command, whereas the RF remote is silent. It is no longer
> a user-controllable capability — there is **no KNX command/status GA and no dashboard chip** for it.
> Instead the hub **force-silences it** on every connect. This document is kept for history.

## What replaced it

The Wind Calm profile declares a connect-time baseline (`DeviceProfile.OnConnectDps = {"66": false}`, set
in `WindCalmProfile`). On each (re)connect `TuyaConnection` **reconciles it against the device's first
state readback** (from the connect-time `DP_QUERY`) and writes **only the DPs whose current value
differs** — it does **not** write blindly:
- Buzzer already off (the normal case; the 3.5 `Windcalm-Windstylance` persists DP 66 = `false`) → **no
  write, no beep.**
- Buzzer genuinely on → one write to silence it (the single, intended beep), then quiet.
- A DP the device doesn't report is skipped; profiles with no `OnConnectDps` write nothing.

> **Why conditional, not blind (learned the hard way).** Live testing showed the 3.5 unit **beeps on
> *any* DP 66 write — even a redundant `{"66":false}` when it's already off** (connecting is silent; the
> *write* is what beeps). A blind write therefore beeped on **every reconnect**, and a
> [heartbeat bug](../../../CLAUDE.md) that flapped the connection every ~10s turned that into a beep every
> ~10s — the original "beeps on reconnect" report. Fixed on both fronts: the heartbeat no longer flaps the
> link, and the baseline is now a conditional reconcile (`TuyaConnection.ComputeBaselineWrites`).

The reconcile outcome is logged at **Information** (`connect-time baseline already satisfied; no write
sent` or `wrote connect-time baseline DPs [...] (state differed)`) so it is confirmable from `docker logs`.

## Hardware limit (some units can't be silenced)

DP 66 is only *advisory* in the Tuya cloud schema — a given MCU may not implement it. Confirmed on the
**`XW-FAN-215-D`** (protocol 3.4): it never reports DP 66 and silently drops a `{"66":false}` write (the
buzzer still sounds on every command). On such units the beep is **not software-controllable**; the only
recourse is physically muting/removing the buzzer. The connect-time reconcile **skips** DP 66 there (the
device never reports it, so there's nothing to match against and nothing is written).

## History

Originally (post-MVP) DP 66 was wired as a full user-controllable `FanBeep` capability — a KNX 1.001
command + status GA (`1/1/11` / `1/1/12`) and a 🔔/🔕 dashboard chip — and later a `DesiredBeep` startup
reconciliation. All of that was removed once it was clear the beep is only ever unwanted: the capability,
its Application command/handler, the profile binding, the dashboard chip, and the config keys are gone.
