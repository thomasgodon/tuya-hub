# UC-10 — Fan confirmation beep from KNX — **REMOVED (nuisance-only; now force-silenced)**

> **This use case has been removed.** The confirmation beep (DP 66 `fan_beep`) was a pure nuisance:
> the device beeps to acknowledge every LAN command, whereas the RF remote is silent. It is no longer
> a user-controllable capability — there is **no KNX command/status GA and no dashboard chip** for it.
> Instead the hub **force-silences it** on every connect. This document is kept for history.

## What replaced it

The Wind Calm profile declares a connect-time baseline write (`DeviceProfile.OnConnectDps = {"66": false}`,
set in `WindCalmProfile`). On each (re)connect, after the state-sync `DP_QUERY`, `TuyaConnection` sends
that raw `CONTROL` once, so no subsequent LAN command beeps. It is a **blind, unconditional** write:
- On a unit that implements DP 66 (e.g. the 3.5 `Windcalm-Windstylance`), the first write silences the
  buzzer and it stays silent; at most one beep on a cold connect.
- Empty for any profile that declares no `OnConnectDps` (other device types write nothing).

## Hardware limit (some units can't be silenced)

DP 66 is only *advisory* in the Tuya cloud schema — a given MCU may not implement it. Confirmed on the
**`XW-FAN-215-D`** (protocol 3.4): it never reports DP 66 and silently drops a `{"66":false}` write (the
buzzer still sounds on every command). On such units the beep is **not software-controllable**; the only
recourse is physically muting/removing the buzzer. The connect-time write still fires there but is a
harmless no-op.

## History

Originally (post-MVP) DP 66 was wired as a full user-controllable `FanBeep` capability — a KNX 1.001
command + status GA (`1/1/11` / `1/1/12`) and a 🔔/🔕 dashboard chip — and later a `DesiredBeep` startup
reconciliation. All of that was removed once it was clear the beep is only ever unwanted: the capability,
its Application command/handler, the profile binding, the dashboard chip, and the config keys are gone.
