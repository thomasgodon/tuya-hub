# UC-08 — Subscribe to state changes via WebSocket

**Summary:** An API client opens a WebSocket to receive real-time device state changes as they
happen, instead of polling the REST API.
**Primary actor:** API client (live dashboard or event-driven integration).
**Stakeholders & interests:** Integrators want low-latency updates; operator wants bounded
resource use for many concurrent subscribers.
**Preconditions:** The WebSocket endpoint is enabled and the client is authorised.
**Trigger:** Client connects to the WebSocket endpoint, e.g. `GET /ws`.

## Main flow
1. tuya-hub authenticates/authorises the connection.
2. The client optionally sends a subscription filter (specific devices/DPs, or all).
3. tuya-hub sends an initial snapshot of the subscribed state (optional, configurable).
4. Whenever a state change is observed (UC-04, UC-05, or UC-06), tuya-hub pushes a change event
   to matching subscribers: device ID, DP ID, new value, source, and timestamp.
5. tuya-hub sends periodic keep-alive/ping frames and drops dead connections.

## Alternate flows
- **2a. No filter supplied:** client receives all state-change events.
- **4a. Backpressure:** a slow client that cannot keep up is throttled or disconnected per a
  documented policy, so it cannot stall the bridge.

## Error scenarios
- Unauthorised connect → reject the upgrade.
- Connection drops → client reconnects and (optionally) requests a fresh snapshot to resync.

## Postconditions
- Subscribed clients hold an eventually-consistent, real-time view of device state.

## Open questions
- Event schema and versioning (shared with REST value representation from UC-07)?
- Delivery guarantee: best-effort fan-out, or per-client buffered replay after reconnect?
