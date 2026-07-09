# UC-02 — Configure a device ↔ KNX mapping

**Summary:** The operator declares how a Tuya device's datapoints map to KNX group addresses, so
the bridge knows what to translate in each direction.
**Primary actor:** Operator.
**Stakeholders & interests:** Operator needs an unambiguous, validated mapping; KNX integrator
needs the group addresses and DPTs to match the ETS project.
**Preconditions:** The device's ID, IP, local key, and protocol version are known (see UC-01).
**Trigger:** Operator edits the configuration file (or runs a config command) and reloads.

## Main flow
1. Operator adds a device entry: ID, IP (or "resolve via discovery"), local key, protocol version.
2. For each Tuya DP to bridge, the operator declares a mapping:
   - the Tuya DP ID and its type (`bool`, `value`, `enum`, `string`, `raw`);
   - the **command** group address (KNX → Tuya), if the DP is writable;
   - the **feedback/status** group address (Tuya → KNX), if the DP is readable;
   - the KNX **DPT** and any scaling/transform (e.g. Tuya 0–1000 ↔ KNX 0–100 %).
3. tuya-hub validates the configuration on load: unique device IDs, group-address syntax,
   DPT/value-range compatibility, no two mappings writing the same DP from conflicting GAs.
4. On success, the mappings become active; on failure, tuya-hub reports the offending entry and
   refuses to apply the change.

## Alternate flows
- **2a. Read-only DP:** only a feedback GA is declared; commands to it are rejected.
- **2b. Enum DP:** operator supplies a value map between Tuya enum strings and KNX values.
- **3a. Hot reload:** an already-running bridge reloads config without dropping unaffected
  device connections where possible.

## Error scenarios
- Invalid local key or protocol version → device will fail to connect (surfaced in UC-04/UC-05).
- Duplicate group address used for unrelated purposes → validation warning.
- Scaling transform that cannot represent the source range → validation error.

## Postconditions
- A validated, active mapping set is loaded in memory and persisted in the config file.

## Open questions
- Config format: YAML, JSON, or a dedicated schema? (Affects tooling and validation messages.)
- Do we support templating for many identical devices (e.g. a fleet of plugs)?
