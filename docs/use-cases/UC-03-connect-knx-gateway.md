# UC-03 — Connect to the KNXnet/IP gateway

**Summary:** tuya-hub establishes and maintains a connection to the KNX bus via a KNXnet/IP
gateway so it can send and receive group telegrams.
**Primary actor:** tuya-hub (system, at startup).
**Stakeholders & interests:** Operator wants a reliable bus link; the KNX installation must not
be flooded or destabilised by the bridge.
**Preconditions:** A KNXnet/IP gateway is reachable, and connection settings (mode, host/port or
multicast group, physical address) are configured.
**Trigger:** Application startup, or loss of an existing bus connection.

## Main flow
1. tuya-hub reads the configured connection mode: **tunnelling** (point-to-point to a gateway)
   or **routing** (multicast).
2. It opens the connection and, for tunnelling, negotiates a tunnel and obtains its assigned
   address.
3. It subscribes to the group addresses referenced by active mappings (or opens a bus monitor,
   depending on strategy).
4. It marks the bus link as **healthy** and begins processing UC-04 and UC-05 traffic.
5. It sends periodic connection keep-alives as required by the transport.

## Alternate flows
- **1a. Auto-discovery of gateways:** if no host is configured for tunnelling, tuya-hub performs
  a KNXnet/IP search and lists candidate gateways.
- **2a. Gateway busy:** all tunnelling connections in use → retry with backoff and report status.

## Error scenarios
- Gateway unreachable / DNS failure → retry with exponential backoff; expose "bus disconnected"
  in health and logs; do not crash.
- Connection drops mid-operation → automatically reconnect and re-subscribe; buffer or drop
  outbound telegrams per a documented policy.
- Physical/individual address conflict on the bus → log a clear error and stop attempting to
  send until resolved.

## Postconditions
- Either a healthy bus connection with active subscriptions, or a clearly-reported disconnected
  state with an ongoing reconnect loop.

## Open questions
- Which KNX stack/library do we build on? (Determines tunnelling vs routing feature support.)
- Reconnect policy: bounded queue of pending telegrams, or drop-and-resync from device state?
