# UC-06 — Control a device via the REST API

**Summary:** An API client sets a device datapoint through tuya-hub's HTTP API, producing the
same effect as a KNX command.
**Primary actor:** API client (dashboard, script, or third-party integration).
**Stakeholders & interests:** Integrators want programmatic control without touching KNX;
operator wants the API to respect the same validation and safety as the KNX path.
**Preconditions:** The REST API is enabled and the client is authorised; a mapping/known DP
exists for the target device; device reachable on the LAN.
**Trigger:** Client sends a write request, e.g. `POST /devices/{id}/dp/{dpId}` with a value.

## Main flow
1. tuya-hub authenticates/authorises the request.
2. It validates the target device and DP, and the supplied value against the DP type/range.
3. It applies any transform and sends the `set` command over the Tuya local protocol
   (shared logic with UC-04).
4. It returns the accepted/new value (and whether confirmation from the device was received).
5. The resulting state change propagates to KNX feedback (UC-05) and to subscribers (UC-08).

## Alternate flows
- **1a. Unauthorised:** respond `401/403` without touching the device.
- **3a. Device offline:** respond `503`/`409` per policy; optionally queue (see UC-09).

## Error scenarios
- Unknown device/DP → `404`.
- Value fails validation → `400` with a descriptive message.
- Device command times out → `502`/`504`; device state left unchanged.

## Postconditions
- On success: device DP updated; KNX feedback and WebSocket subscribers reflect the change.

## Open questions
- Auth model: API key, token, mTLS, or LAN-only trust?
- Do REST-originated changes get a distinct source tag for audit/loop-suppression?
