# Zoom Room Configuration Authoring

> **Configuration boundary.** Examples are structural templates, not deployable credentials. Replace only documented placeholders; never commit credentials, activation codes, or site-specific addresses. The source and its configuration-deserialization tests are authoritative when this guide conflicts with any older document.

## What this configuration controls

The Zoom Room plug-in configuration selects monitoring, call/layout defaults, camera capability flags, phone-dial behavior, and local SDK configuration. It is the codec device that the room may reference through `videoCodecs` and participant camera mappings.

| Authoring fact | Verified source contract |
|---|---|
| Configured type alias | `zoomroom` |
| Primary source authority | [`../src/ZoomRoomPropertiesConfig.cs`](../src/ZoomRoomPropertiesConfig.cs) |
| Use this guide when | adding a codec, enabling codec-driven camera behavior, changing default layouts, or integrating codec audio/video with room lists and tielines. |

## Why the relationships matter

The device key must match room video-codec and participant codec-camera references. Its source and destination list entries must use the correct list-item keys; codec content and camera destination ports require routing/tieline compatibility with the selected matrix.

## Source-declared configuration facts

`CommunicationMonitorProperties`, `DisablePhonebookAutoDownload`, `SupportsCameraAutoMode`, `SupportsCameraOff`, `AutoDefaultLayouts`, default sharing/call layouts, `MinutesBeforeMeetingStart`, `ActivationCode`, `PhoneDialMode`, and `SdkConfigPath` are declared by the properties class.

## Safe structural example

```json
{
  "key": "conference-codec",
  "name": "Conference Codec",
  "type": "zoomroom",
  "properties": {
    "communicationMonitorProperties": { "warningTimeoutMs": 5000, "errorTimeoutMs": 10000 },
    "disablePhonebookAutoDownload": false,
    "supportsCameraAutoMode": true,
    "supportsCameraOff": true,
    "autoDefaultLayouts": true,
    "minutesBeforeMeetingStart": 5,
    "phoneDialMode": "<supported-mode>",
    "sdkConfigPath": "<processor-local-sdk-config-path>"
  }
}
```

## When and how to author it

Start with the closest repository example or a known-good template. Add the device with a stable unique `key`, use the exact factory alias, and populate only the properties needed by the selected capabilities. Build every key relationship before deployment; do not rely on permissive JSON deserialization or case-insensitive type aliases to repair a wrong design.

## Validation

Verify the factory resolves `zoomroom`, confirm the codec device is online, then validate the room’s codec mapping, one camera selection, and one receive/transmit audio control path. Use a non-production meeting or disconnected test state before authorizing call-control tests.

## Change and safety rules

Do not commit `ActivationCode`, SDK files, local paths, meeting information, or addresses. Layout enum names and dial modes must be validated against the version actually deployed.

## Sources

- [`../src/ZoomRoomPropertiesConfig.cs`](../src/ZoomRoomPropertiesConfig.cs)
- `tests/ConfigDeserializationTests.cs` and `tests/FactoryDiscoveryTests.cs`, where present
