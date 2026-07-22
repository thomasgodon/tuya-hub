# tuya-hub

A generic **Tuya → KNX** hub. Each Tuya device is controlled **locally over the LAN** (Tuya local
protocol, per-device local key — **no cloud**) and exposed as **KNX group objects** for bidirectional
control and status feedback. It runs as a .NET 10 console/worker host with an optional read-only status
dashboard.

Device types are pluggable via **profiles**. The **CREATE / IKOHS "Wind Calm"** ceiling-fan-with-light
(Tuya category `fsd`, local protocol **3.3**) is the first and currently only profile.

- **What to build / why:** [`docs/PRD-MVP.md`](docs/PRD-MVP.md)
- **Wind Calm device reference** (datapoints, KNX group-object model, protocol quirks):
  [`docs/use-cases/wind-calm/README.md`](docs/use-cases/wind-calm/README.md)

This README is the configuration guide: get credentials → configure the device → map to KNX → run.

## Prerequisites

- A host on the **same LAN/VLAN broadcast domain** as the Tuya devices.
- A **KNXnet/IP gateway** reachable for IP tunnelling (default UDP `3671`).
- Each device's **`DeviceId`**, **`LocalKey`**, and **IP address** (see Step 1).
- Don't run the **Smart Life / Tuya app** against a device while tuya-hub is connected: the module accepts
  only ~3 concurrent TCP sockets on port 6668, and tuya-hub keeps one persistent socket per device.

## Step 1 — Obtain each device's `DeviceId`, `LocalKey`, and IP

The **`LocalKey`** is a per-device secret. You extract it **once** (it requires the Tuya cloud), after
which tuya-hub uses it **only locally** — no cloud connection at runtime.

### Recommended: the `tinytuya` wizard

1. Pair the device in the **Smart Life** (or Tuya) mobile app so it is bound to your Tuya account.
2. Create a free **Tuya IoT Platform** cloud project (<https://iot.tuya.com>), and **link your Smart Life
   account** to it (Cloud → link app account / "Link Tuya App Account").
3. Install tinytuya and run the wizard:
   ```pwsh
   pip install tinytuya
   python -m tinytuya wizard
   ```
   Enter the project's API key/secret and region when prompted. The wizard writes `devices.json` listing
   each device's `id` (→ **`DeviceId`**), `key` (→ **`LocalKey`**), and `ip`.
4. Confirm reachability / find IPs on the LAN:
   ```pwsh
   python -m tinytuya scan
   ```

### Finding IP + DeviceId without tinytuya (LocalKey still manual)

tuya-hub's own **LAN discovery** (dashboard, [UC-01](docs/use-cases/UC-01-discover-tuya-devices.md)) passively
lists devices broadcasting on the LAN — showing `DeviceId`, IP, and protocol version, tagged **"needs local
key"**. The beacon never carries the `LocalKey`, so you still supply that from the wizard above.

**Caveats**

- Re-pairing a device in the app can **rotate its `LocalKey`** — re-run the wizard if a device stops
  connecting with an "invalid key" error.
- Only **protocol 3.3** devices are supported; 3.5-firmware units use framing tuya-hub can't decode.

## Step 2 — Configure the device

There are two config paths using the **same keys**: edit `appsettings.json` directly (bare-metal / dev), or
supply environment variables via `.env` (Docker). Env vars override `appsettings.json`.

### `TuyaHub/appsettings.json`

The shipped file ships everything **disabled** (`Enabled: false`). Fill in a device and enable it:

```jsonc
{
  "TuyaOptions": {
    "PollIntervalSeconds": 10,
    "HeartbeatIntervalSeconds": 10,
    "LivenessTimeoutSeconds": 30,
    "ConnectTimeoutSeconds": 5,
    "ReconnectInitialBackoffSeconds": 1,
    "ReconnectMaxBackoffSeconds": 30,
    "Devices": [
      {
        "Name": "LivingRoomFan",
        "Profile": "wind-calm",
        "Enabled": true,
        "IpAddress": "192.168.0.50",
        "DeviceId": "REPLACE_WITH_DEVICE_ID",
        "LocalKey": "REPLACE_WITH_LOCAL_KEY",
        "ProtocolVersion": "3.3",
        "Port": 6668
      }
    ]
  }
}
```

Per-device fields (`TuyaOptions.Devices[]`):

| Field | Meaning | Default |
|-------|---------|---------|
| `Name` | Stable key; ties the device to its `DeviceMappings` entry. **Required.** | — |
| `Profile` | Device type / profile id. | `"wind-calm"` |
| `Enabled` | Whether tuya-hub connects to this device. | `true` in code, ships `false` |
| `IpAddress` | Device LAN IP. | — |
| `DeviceId` | Tuya device id (`gwId`). | — |
| `LocalKey` | Per-device local secret (Step 1). | — |
| `ProtocolVersion` | Local protocol version. | `"3.3"` |
| `Port` | Tuya local TCP port. | `6668` |

Add a second device by appending another element to the `Devices` array (each with its own `Name`).

Global `TuyaOptions` tunables:

| Key | Meaning | Default |
|-----|---------|---------|
| `PollIntervalSeconds` | `DP_QUERY` poll cadence (catches RF-remote changes that don't push). | `10` |
| `HeartbeatIntervalSeconds` | Keep-alive interval. | `10` |
| `LivenessTimeoutSeconds` | Watchdog force-reconnect if no inbound byte within this window (must exceed heartbeat). | `30` |
| `ConnectTimeoutSeconds` | TCP connect timeout per attempt. | `5` |
| `ReconnectInitialBackoffSeconds` / `ReconnectMaxBackoffSeconds` | Reconnect backoff bounds. | `1` / `30` |

### `.env` (Docker)

Copy `.env.example` → `.env` (git-ignored) and set real values. Config binds via .NET's **double-underscore**
env-var convention:

```bash
TuyaOptions__Devices__0__Enabled=true
TuyaOptions__Devices__0__Name=LivingRoomFan
TuyaOptions__Devices__0__Profile=wind-calm
TuyaOptions__Devices__0__IpAddress=192.168.0.50
TuyaOptions__Devices__0__DeviceId=REPLACE_WITH_DEVICE_ID
TuyaOptions__Devices__0__LocalKey=REPLACE_WITH_LOCAL_KEY
TuyaOptions__Devices__0__ProtocolVersion=3.3
TuyaOptions__Devices__0__Port=6668
```

A second device uses index `__1__`, a third `__2__`, and so on. KNX mappings are keyed by device `Name`:
`DeviceMappings__<Name>__<MappingKey>` (see Step 3).

## Step 3 — Map the device to KNX

Configure the gateway (`KnxOptions`):

| Key | Meaning | Default |
|-----|---------|---------|
| `Enabled` | Enable the KNX bus. Set `false` to disable KNX entirely. | ships `false` |
| `Host` | KNXnet/IP gateway IP (IP tunnelling). | — |
| `Port` | Gateway port. | `3671` |
| `IndividualAddress` | tuya-hub's KNX physical address. | e.g. `1.1.100` |
| `ReconnectInitialBackoffSeconds` / `ReconnectMaxBackoffSeconds` | Bus reconnect backoff bounds. | `1` / `30` |

Then map each device function to a KNX group address under `DeviceMappings.<Name>`. **Command** (KNX → device)
and **status** (device → KNX) are **always separate** group addresses. A **missing or empty GA string
disables** that function.

```jsonc
"DeviceMappings": {
  "LivingRoomFan": {
    "FanPower": "1/1/1",              "FanPowerStatus": "1/1/2",
    "FanSpeedStep": "1/1/3",          "FanSpeedStatus": "1/1/4",
    "FanDirection": "1/1/5",          "FanDirectionStatus": "1/1/6",
    "FanTimer": "1/1/7",              "FanTimerStatus": "1/1/8",
    "LightPower": "1/1/9",            "LightPowerStatus": "1/1/10",
    "LightCct": "1/1/13",             "LightCctStatus": "1/1/14",
    "LightCctStep": "1/1/16",
    "AvailabilityStatus": "1/1/15"
  }
}
```

Valid mapping keys for the **wind-calm** profile, with their Tuya DP and KNX DPT:

| Mapping key | Direction | Tuya DP | KNX DPT |
|-------------|-----------|---------|---------|
| `FanPower` / `FanPowerStatus` | ⇄ | 60 | 1.001 switch |
| `FanSpeedStep` | KNX → device | 62 | **3.007** dim step (relative, no feedback) |
| `FanSpeedStatus` | device → KNX | 62 | **5.010** count (1–6; 0 = off) |
| `FanDirection` / `FanDirectionStatus` | ⇄ | 63 | 1.001 (0 = forward/summer, 1 = reverse/winter) |
| `FanTimer` / `FanTimerStatus` | ⇄ | 64 | **7.006** minutes (0–540) |
| `LightPower` / `LightPowerStatus` | ⇄ | 20 | 1.001 switch |
| `LightCct` / `LightCctStatus` | ⇄ | 23 | **5.001** % → 3 discrete steps |
| `LightCctStep` | KNX → device | 23 | **3.007** dim step (relative long-press cycle) |
| `AvailabilityStatus` | device → KNX | — (connectivity-driven, no DP) | 1.001 switch |

Notes:

- **Fan speed is relative.** The command GA is a 3.007 dim-step telegram (±1 level), so current speed is
  reported on the **separate** 5.010 status GA. Dim-up from off turns the fan on at level 1; dim-down at
  level 1 stays at 1 (power off is only via `FanPower`).
- **The light is on/off + CCT only — no dimming.** The Wind Calm hardware does not honour a brightness
  write (DP 22), so brightness is not exposed. Use `LightPower` for on/off and `LightCct` for the
  3-step colour temperature.
- **CCT can also be cycled by a long-press.** `LightCctStep` is an optional relative 3.007 command
  (same DP 23) for a KNX pushbutton: a long-press cycles cool → warm-white → warm → cool …, wrapping at
  the rails. It coexists with the absolute `LightCct`; both are optional and independent, and the
  resulting step is reported on the shared `LightCctStatus`.
- **Light CCT is flicker-prone.** Leave `LightCct`/`LightCctStatus` empty to skip it; the light then
  behaves as on/off-only.
- See [`docs/use-cases/wind-calm/README.md`](docs/use-cases/wind-calm/README.md) for the full datapoint
  reference and firmware quirks (integer-only fan speed, MCU-owned timer, RF-remote state drift).

## Step 4 — Dashboard (optional)

`DashboardOptions` gates a **read-only** status page (and LAN discovery):

| Key | Meaning | Default |
|-----|---------|---------|
| `Enabled` | Serve the dashboard + run LAN discovery. `false` = headless worker, no HTTP endpoint. | `true` |
| `Port` | HTTP port. | `8080` |

When enabled, browse to `http://<host>:8080/` for live per-device state plus a list of unconfigured Tuya
devices discovered broadcasting on the LAN.

## Step 5 — Run & verify

**Docker** (recommended — see [CLAUDE.md](CLAUDE.md) "Running in Docker"):

```pwsh
docker compose pull
docker compose up -d
```

Requires `network_mode: host` (KNXnet/IP tunnelling and Tuya UDP discovery beacons need it); allow inbound
UDP `6666`/`6667` on the host firewall for the "Discovered" list to populate.

**Local dev:**

```pwsh
dotnet run --project TuyaHub
```

**Verify:**

1. Dashboard (`http://<host>:8080/`) shows the device **online**.
2. Send to a command GA (e.g. `FanPower`) from ETS / a KNX switch and confirm the fan reacts and the
   matching status GA + dashboard update.
3. Change the device with its physical RF remote and confirm the status GA updates within
   `PollIntervalSeconds`.
4. On trouble, check logs (`docker compose logs -f`) for connection / backoff / invalid-key messages.

## Adding more devices or device types

- **Another device:** add a `Devices[]` element (or `__N__` env vars) and a matching `DeviceMappings.<Name>`
  block — no rebuild needed.
- **A new device type:** register a new profile (its Tuya DP codec, KNX bindings, and aggregate). See the
  profile architecture in [CLAUDE.md](CLAUDE.md) and [`docs/PRD-MVP.md`](docs/PRD-MVP.md).
