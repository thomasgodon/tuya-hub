# UC-07 — Query device state via the REST API

**Summary:** An API client reads the current known state of one or more devices from tuya-hub.
**Primary actor:** API client.
**Stakeholders & interests:** Integrators need a fast, consistent read model; operator wants
reads not to hammer devices on the LAN.
**Preconditions:** The REST API is enabled and the client is authorised.
**Trigger:** Client sends a read request, e.g. `GET /devices` or `GET /devices/{id}`.

## Main flow
1. tuya-hub authenticates/authorises the request.
2. It serves device metadata (ID, name, protocol version, online status) and the cached DP
   values maintained by UC-04/UC-05.
3. Each value includes a freshness indicator (last-updated timestamp, and online/offline).
4. It returns the response as JSON.

## Alternate flows
- **2a. Force refresh:** a query flag triggers a live read of the device before responding
  (bounded by a timeout; falls back to cache on failure).
- **2b. Filtering:** client filters by device, DP, or online status.

## Error scenarios
- Unknown device → `404`.
- Force-refresh times out → return cached value with a `stale` flag rather than failing.

## Postconditions
- No device state is changed (safe, read-only) unless force-refresh performed a live read.

## Open questions
- Should the read model expose raw Tuya DP values, the KNX-scaled values, or both?
- Pagination for large device fleets?
