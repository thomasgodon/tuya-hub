# UC-01 — Discover Tuya devices on the LAN

**Summary:** The operator asks tuya-hub to find Tuya devices that are broadcasting on the local
network, so they can be added to the configuration.
**Primary actor:** Operator (person setting up the bridge).
**Stakeholders & interests:** Operator wants an accurate, low-effort inventory; end users want
their devices to end up controllable.
**Preconditions:** tuya-hub runs on a host in the same broadcast domain (LAN/VLAN) as the devices.
**Trigger:** Operator runs the discovery command (e.g. `tuya-hub discover`).

## Main flow
1. tuya-hub listens for Tuya UDP broadcast beacons on the standard discovery ports.
2. For each beacon received it records the device ID (`gwId`), IP address, and advertised
   protocol version.
3. It de-duplicates by device ID across the listen window.
4. After the listen window elapses, it prints a table of discovered devices: ID, IP, protocol
   version, and whether a local key / mapping is already known for that ID.
5. Operator uses the output to proceed with UC-02 (configuration).

## Alternate flows
- **1a. Encrypted beacons (protocol 3.4/3.5):** payload is not readable without keys; tuya-hub
  still reports ID/IP/version and flags the device as "needs local key".
- **4a. No devices found:** tuya-hub reports zero results and hints at common causes (wrong
  VLAN, client isolation on the AP, host firewall blocking the UDP port).

## Error scenarios
- UDP discovery port already bound by another process → report the conflict and exit non-zero.
- Host has multiple interfaces → discovery may miss devices on the interface not listened on;
  allow the operator to select an interface.

## Postconditions
- A list of currently-reachable device IDs and IPs is available to the operator.
- No configuration is modified by discovery alone (read-only operation).

## Open questions
- Should discovery persist results to a cache file, or always be live?
- Do we auto-enrich with local keys pulled from a configured Tuya Cloud account, or stay fully
  offline and require the operator to supply keys manually?
