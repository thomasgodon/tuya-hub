# UC-01 — Discover Tuya devices on the LAN

**Summary:** The operator asks tuya-hub to find Tuya devices that are broadcasting on the local
network, so they can be added to the configuration.
**Primary actor:** Operator (person setting up the bridge).
**Stakeholders & interests:** Operator wants an accurate, low-effort inventory; end users want
their devices to end up controllable.
**Preconditions:** tuya-hub runs on a host in the same broadcast domain (LAN/VLAN) as the devices,
with the dashboard enabled (`DashboardOptions.Enabled=true`).
**Trigger:** tuya-hub is running with the dashboard enabled; discovery runs continuously in the
background and the result is shown live on the dashboard.

## Main flow
1. tuya-hub listens for Tuya UDP broadcast beacons on the standard discovery ports (6666/6667) via its
   own `TuyaLanDiscoveryListener` (reusing TuyaNet's codec but owning the receive loop). Each beacon is
   decoded inside a per-packet `try/catch`, so an undecodable beacon (a protocol-3.5 `00 00 66 99` frame
   or stray/malformed UDP) is logged and skipped rather than crashing the host — the reason we no longer
   use TuyaNet's `TuyaScanner`, whose library-owned thread rethrew such failures fatally.
2. For each beacon received it records the device ID (`gwId`), IP address, advertised protocol
   version, and product key.
3. It de-duplicates by device ID and prunes entries whose beacon stops arriving, so the list reflects
   currently-reachable devices.
4. The dashboard shows a live "Discovered" section listing devices that are **not** already in the
   configured device list (matched by device ID / `gwId`): ID, IP, protocol version, product key, and
   a "needs local key" tag. Devices already configured never appear here.
5. Operator uses the display to proceed with UC-02 (configuration).

## Alternate flows
- **1a. Protocol 3.1/3.3/3.4 beacons:** decoded with the universal (non-secret) discovery key; tuya-hub
  reports ID/IP/version/product key and flags the device as "needs local key" (the beacon never carries it).
- **1b. Protocol 3.5 beacons:** use the newer `00 00 66 99` / AES-GCM framing the codec predates, so they
  are skipped and such devices do not appear in the "Discovered" list (they no longer crash discovery).
- **4a. No devices found:** tuya-hub reports zero results and hints at common causes (wrong
  VLAN, client isolation on the AP, host firewall blocking the UDP port).

## Error scenarios
- UDP discovery port already bound by another process → report the conflict and exit non-zero.
- Host has multiple interfaces → discovery may miss devices on the interface not listened on;
  allow the operator to select an interface.

## Postconditions
- A list of currently-reachable, unconfigured device IDs and IPs is shown on the dashboard.
- No configuration is modified by discovery alone (read-only operation).

## Resolved decisions
- **Live, not cached:** the discovered list is an in-memory, TTL-pruned view of what is currently
  broadcasting — it is not persisted to a cache file.
- **Fully offline:** no Tuya Cloud enrichment. The beacon never carries the local key, so discovered
  devices are flagged "needs local key" and the operator supplies the key manually (UC-02).
- **Surface:** discovery is exposed only on the read-only web dashboard (gated by
  `DashboardOptions.Enabled`), not as a separate CLI command.
