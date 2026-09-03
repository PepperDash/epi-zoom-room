![PepperDash Essentials Pluign Logo](/images/essentials-plugin-blue.png)

# Essentials Plugin Template (c) 2023

## License

Provided under MIT license

## Overview

Fork this repo when creating a new plugin for Essentials. For more information about plugins, refer to the Essentials Wiki [Plugins](https://github.com/PepperDash/Essentials/wiki/Plugins) article.

This repo contains example classes for the three main categories of devices:
* `EssentialsPluginTemplateDevice`: Used for most third party devices which require communication over a streaming mechanism such as a Com port, TCP/SSh/UDP socket, CEC, etc
* `EssentialsPluginTemplateLogicDevice`:  Used for devices that contain logic, but don't require any communication with third parties outside the program
* `EssentialsPluginTemplateCrestronDevice`:  Used for devices that represent a piece of Crestron hardware

There are matching factory classes for each of the three categories of devices.  The `EssentialsPluginTemplateConfigObject` should be used as a template and modified for any of the categories of device.  Same goes for the `EssentialsPluginTemplateBridgeJoinMap`.

This also illustrates how a plugin can contain multiple devices.

## Cloning Instructions

After forking this repository into your own GitHub space, you can create a new repository using this one as the template.  Then you must install the necessary dependencies as indicated below.

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries are required. They referenced via nuget. You must have nuget.exe installed and in the `PATH` environment variable to use the following command. Nuget.exe is available at [nuget.org](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe).

### Installing Dependencies

To install dependencies once nuget.exe is installed, run the following command from the root directory of your repository:
`nuget install .\packages.config -OutputDirectory .\packages -excludeVersion`.
Alternatively, you can simply run the `GetPackages.bat` file.
To verify that the packages installed correctly, open the plugin solution in your repo and make sure that all references are found, then try and build it.

### Installing Different versions of PepperDash Core

If you need a different version of PepperDash Core, use the command `nuget install .\packages.config -OutputDirectory .\packages -excludeVersion -Version {versionToGet}`. Omitting the `-Version` option will pull the version indicated in the packages.config file.

### Instructions for Renaming Solution and Files

See the Task List in Visual Studio for a guide on how to start using the template.  There is extensive inline documentation and examples as well.

For renaming instructions in particular, see the XML `remarks` tags on class definitions

## Build Instructions (PepperDash Internal) 

## Generating Nuget Package 

In the solution folder is a file named "PDT.EssentialsPluginTemplate.nuspec" 

1. Rename the file to match your plugin solution name 
2. Edit the file to include your project specifics including
    1. <id>PepperDash.Essentials.Plugin.MakeModel</id> Convention is to use the prefix "PepperDash.Essentials.Plugin" and include the MakeModel of the device. 
    2. <projectUrl>https://github.com/PepperDash/EssentialsPluginTemplate</projectUrl> Change to your url to the project repo

There is no longer a requirement to adjust workflow files for nuget generation for private and public repositories.  This is now handled automatically in the workflow.

__If you do not make these changes to the nuspec file, the project will not generate a nuget package__

## Console Commands

The plugin registers the following console commands (operator access level):

| Command | Description |
|---------|-------------|
| `pairZoomRoom <activation-code>` | Pair the Zoom Room using the supplied activation code. |
| `repairZoomRoom` | Reconnect to the last paired Zoom Room using stored credentials. Reconnect is normally **automatic** (see Connection watchdog below); this is the manual override. |
| `unpairZoomRoom` | Unpair from the Zoom Room. |
| `forceRepairZoom` | Clear stored credentials and re-pair using the activation code from configuration. Use this after rotating the activation code, when stored credentials would otherwise be reused. |

## Commissioning: pairing a new system

Every Zoom Room resource must be paired with Zoom's cloud once, using a one-time **activation code**, before the plugin can log in and start reporting meeting status. Follow this procedure when commissioning a system for the first time.

1. **Generate the activation code in Zoom.** In the Zoom web portal, go to **Room Management > Zoom Rooms**, select (or add) the Room resource for this system, and generate an activation code from its **Room Profile > Edit / Activate** page. The code is single-use and expires after a short window (check the portal for the exact expiration), so generate it right before you're ready to pair.
2. **Pair the room.** Choose one of the following:
    - **Recommended for first-time commissioning:** at the processor console, run:
      ```
      pairZoomRoom <activation-code>
      ```
      This pairs immediately without requiring a program restart or a config change.
    - **Alternative:** set `activationCode` in the device's `properties` block in the configuration file, then load/restart the program. On startup, if there are no stored pairing credentials yet, the plugin automatically calls `PairRoomWithActivationCode` using the configured value.
3. **Verify pairing succeeded.** Watch the console/error log for the pairing result. Once paired, the room can be confirmed via the SDK connection state or by checking meeting status.
4. **Re-pairing after a factory reset or activation code rotation.** Stored credentials are reused automatically on every subsequent startup/reconnect (see Connection watchdog below), so a fresh activation code is only needed if the room was unpaired, or Zoom invalidated/rotated the credentials. In that case:
    - Generate a new activation code in the Zoom portal as in step 1.
    - Run `pairZoomRoom <new-activation-code>` to pair with the new code directly, **or**
    - Update `activationCode` in configuration and run `forceRepairZoom`, which clears the stale stored credentials and re-pairs using the newly configured code.

> **Note:** Leaving `activationCode` in configuration after initial commissioning is harmless — it is only consulted when there are no stored credentials (first boot) or when `forceRepairZoom` is invoked explicitly.

## Behavior notes

### `MeetingInfo.CanRecord`
`CanRecord` reflects the ZRC SDK's `MeetingRecordingInfo.canIRecord` — **whether _this room_ can start recording** (the room's own ability). Because a **host can always record**, `CanRecord` stays `true` while the room is host and **does not track the "Record to computer" switch in Zoom's Host-tools menu**. That switch is a *participant* permission (`RecordingPermissionTypeLocalRecording`) governing whether attendees may record locally — it is not the room's own ability, and the plugin does not currently surface the participant recording-permission states. (Toggling it off while hosting will not change `CanRecord`; this is expected.)

> **Note:** Crestron console command names cannot be a complete prefix of another registered command, so the force re-pair command is named `forceRepairZoom` rather than `repairZoomRoomConfig` (which would collide with `repairZoomRoom`).

### Connection watchdog / auto-repair
The plugin actively monitors the SDK connection and **self-heals silent/half-open drops** — cases where the network dies without a clean close, leaving the SDK reporting connected while it is actually dead (devcomm would otherwise sit stuck at `IsOk`).

- **Detection:** a 30 s liveness poll plus consecutive command-failure strikes trigger a real `GetMeetingStatus()` probe; a failed probe marks the device offline in devcomm (`InError`).
- **Repair:** auto-reconnect with escalating backoff (`5 → 10 → 20 → 30 → 60 s`, capped at 60 s) that **never gives up** until the room is reachable again — no program restart or manual `repairZoomRoom` needed.

See [docs/connection-watchdog-silent-disconnect.md](docs/connection-watchdog-silent-disconnect.md) for the full write-up (issue, replication, fix, and lab validation).