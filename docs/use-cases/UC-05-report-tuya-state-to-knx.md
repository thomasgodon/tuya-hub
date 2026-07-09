# UC-05 — Report Tuya device state to KNX

**Summary:** When a Tuya device's datapoint changes — from any cause — tuya-hub publishes the
new value to the mapped KNX feedback group address.
**Primary actor:** tuya-hub (reacting to device-originated state).
**Stakeholders & interests:** KNX visualisations and logic need accurate, timely feedback;
end users expect physical changes (e.g. pressing a button on the device) to reflect in KNX.
**Preconditions:** Bus connection healthy (UC-03); a mapping with a feedback GA exists (UC-02);
tuya-hub holds an open/pollable connection to the device.
**Trigger:** A Tuya DP value change is observed (pushed status update, poll result, or the
result of a UC-04 command).

## Main flow
1. tuya-hub observes a new value for a Tuya DP on a device.
2. It matches the (device, DP) pair to a mapping with a feedback group address.
3. It applies the inverse transform/scaling to produce the KNX value (e.g. 0–1000 → 0–100 %).
4. It encodes the value with the mapping's DPT.
5. It writes the value to the feedback group address on the bus (GroupValueWrite).
6. It updates its in-memory device state cache (also served via UC-07/UC-08).

## Alternate flows
- **1a. Push not supported / unreliable:** tuya-hub polls the device on an interval and derives
  changes by diffing against the cached state.
- **5a. Send only on change:** unchanged values are suppressed to avoid needless bus traffic
  (configurable; some setups want periodic refresh writes).

## Error scenarios
- Value cannot be encoded in the target DPT (out of range/precision) → clamp or skip and log.
- Bus disconnected at send time → buffer per policy or drop and resync on reconnect (UC-03).
- Device reports a DP that has no mapping → ignore (optionally log for mapping discovery).

## Postconditions
- The KNX feedback GA reflects the current device value, and the state cache is up to date.

## Open questions
- Poll interval and its impact on device/LAN load for push-incapable devices.
- Startup behaviour: proactively read all mapped DPs and publish initial feedback?
