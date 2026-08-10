# Architecture & Design

High-level system design, fork strategy, and component overview for the epi-bic-zoomroom plugin.

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         CP4N (Crestron)                          │
│                    Beincourt AV Control System                   │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       │ SIMPL+ / Join Map
                       │
         ┌─────────────┴──────────────┐
         │                            │
    ┌────▼──────────────┐    ┌───────▼────────┐
    │  epi-beincourt-   │    │   Other AV     │
    │   room Plugin     │    │   Devices      │
    │  (Main Control)   │    │  (Displays,    │
    └────────┬──────────┘    │   Switches)    │
             │               └────────────────┘
             │
             │ Zoom Room Interface
             │
         ┌───▼──────────────────────────┐
         │  epi-bic-zoomroom Plugin      │  ← YOU ARE HERE
         │  (Zoom Room Control)          │
         │                               │
         │  ┌───────────────────────┐    │
         │  │ ZoomRoom Class        │    │
         │  │ - Control Logic       │    │
         │  │ - State Management    │    │
         │  │ - Routing Integration │    │
         │  └───────┬───────────────┘    │
         │          │                    │
         │  ┌───────▼───────────────┐    │
         │  │ ZrcSdkController      │    │
         │  │ - SDK Communication   │    │
         │  │ - Command Parsing     │    │
         │  │ - Event Handling      │    │
         │  └───────┬───────────────┘    │
         │          │                    │
         │  ┌───────▼───────────────┐    │
         │  │ ZoomRoomJoinMap       │    │
         │  │ - SIMPL Bridge        │    │
         │  │ - Feedback Mapping    │    │
         │  └───────────────────────┘    │
         └───────────┬──────────────────┘
                     │
                     │ TCP Socket
                     │
         ┌───────────▼──────────────┐
         │   Zoom Room (Hardware)   │
         │                          │
         │ - Camera Control         │
         │ - Audio Control          │
         │ - Screen Sharing         │
         │ - Layout Management      │
         └──────────────────────────┘
```

---

## Component Breakdown

### 1. ZoomRoom Class (`src/ZoomRoom.cs`)
**Purpose**: Main plugin device class that represents the Zoom Room hardware

**Key Responsibilities**:
- Implement Essentials device interfaces (IRoutingInputPort, ICurrentSources, etc.)
- Manage connection state and lifecycle
- Expose audio, video, and layout controls
- Handle presentation mode and sharing
- Provide feedback to the SIMPL bridge

**Key Methods**:
- `StartSharing()` - Start content sharing (uses StartSharingOnlyMeeting for Beincourt)
- `SetVolume()` / `VolumeUp()` / `VolumeDown()` - Audio control
- `SetLayout()` - Change meeting layout
- `DismissShareInstruction()` / `ShowShareInstruction()` - Share UI control

**Key Events**:
- `OnMeetingStatusChanged` - Meeting state updates
- `OnParticipantListChanged` - Roster updates
- `OnCameraListChanged` - Available cameras update

### 2. ZrcSdkController Class (`src/Controller/ZrcSdkController.cs`)
**Purpose**: Handles low-level communication with the Zoom Room SDK

**Key Responsibilities**:
- Initialize SDK connection using activation code
- Send commands to the Zoom Room hardware
- Parse and process SDK responses
- Handle reconnection and error recovery
- Extract embedded native wrapper library

**Key Methods**:
- `Connect()` - Establish SDK connection
- `Disconnect()` - Close SDK connection
- `SendCommand()` - Send command to hardware
- `HandleZrcEvent()` - Process incoming events from SDK

### 3. ZoomRoomJoinMap Class (`src/ZoomRoomJoinMap.cs`)
**Purpose**: Maps device state and commands to SIMPL+ join numbers

**Key Responsibilities**:
- Define all join points for SIMPL integration
- Map analog, digital, and serial channels
- Route CP4N commands to device methods
- Provide feedback on device state

**Key Sections**:
- Digital Joins (buttons): 1-100+ (commands and feedback)
- Analog Joins (faders): 200+ (volume, layout, etc.)
- Serial Joins: 300+ (text feedback, status)

---

## Data Flow Examples

### Example 1: Muting the Microphone

```
CP4N (SIMPL)
    │
    ├─ Digital Join #45 (Mute Mic)
    │
    ▼
ZoomRoomJoinMap
    │
    ├─ Maps to MuteUnmuteMicrophone event
    │
    ▼
ZoomRoom.SetMicrophonePrivacy(true)
    │
    ├─ Updates internal state
    ├─ Sends command to SDK
    │
    ▼
ZrcSdkController.SendCommand("MuteUserAudio")
    │
    ├─ Sends over TCP socket
    │
    ▼
Zoom Room Hardware
    │
    ├─ Processes command
    ├─ Mutes microphone
    │
    ▼
SDK Event Response
    │
    ├─ Received by ZrcSdkController
    │
    ▼
ZoomRoom.OnMicrophonePrivacyChanged event fired
    │
    ├─ Updates state
    │
    ▼
ZoomRoomJoinMap feedback
    │
    ├─ Digital Join #45 feedback sent
    │
    ▼
CP4N receives feedback
    ├─ LED updates (mute indicator on)
```

### Example 2: Starting a Presentation

```
CP4N (SIMPL)
    │
    ├─ Digital Join #30 (Start Sharing)
    │
    ▼
ZoomRoom.StartSharing()
    │
    ├─ Calls StartSharingOnlyMeeting()  ← BEINCOURT CUSTOMIZATION
    │
    ▼
ZrcSdkController.SendCommand("StartSharingOnlyMeeting")
    │
    ├─ Zoom Room enters sharing mode
    │
    ▼
SDK Event: ShareStatusOn
    │
    ├─ Processed by ZoomRoom
    │
    ▼
ZoomRoomJoinMap feedback
    ├─ Digital Join #30 feedback ON
    ├─ Analog Join #210 (Layout) updates
    │
    ▼
CP4N receives feedback
    ├─ Sharing indicator lights up
    ├─ Layout options update
```

---

## Fork Architecture

This repository is a **fork of PepperDash/epi-zoom-room** with Beincourt-specific customizations.

### Branch Strategy

```
upstream/main (read-only)
    │
    ├── Public PepperDash releases
    │
    ▼
fork/main
    │
    ├── Synced with upstream/main
    │
    ▼
fork/csv-zoom-sandbox ← **YOU WORK HERE**
    │
    ├── All Beincourt customizations
    ├── Cherry-picked upstream fixes
    ├── Branch protection: requires PR review + tests pass
```

### The Customization: StartSharingOnlyMeeting

**File**: `src/ZoomRoom.cs` (line 1251)

**Original (upstream)**:
```csharp
public override void StartSharing()
{
    _controller.ShareBlackMagic(true, true);
}
```

**Beincourt (this fork)**:
```csharp
public override void StartSharing()
{
    StartSharingOnlyMeeting();
}
```

**Rationale**:
- `ShareBlackMagic()` - Shares content locally (HDMI focus)
- `StartSharingOnlyMeeting()` - Shares with meeting participants (remote focus)
- Beincourt courtroom prioritizes sharing with remote participants

**Impact**:
- When annotation mode is activated, content is shared with all meeting participants
- Better UX for remote court observers

See [FORK-STRATEGY.md](FORK-STRATEGY.md) for details on maintaining this fork.

---

## Essentials Framework Integration

This plugin integrates with the PepperDash Essentials framework:

### Interfaces Implemented

| Interface | Purpose |
|-----------|---------|
| `IBasicCommunicationDevice` | Basic device lifecycle (connect, disconnect) |
| `IRoutingInputPort` | Makes Zoom Room a routing source |
| `ICurrentSources` | Reports current video source |
| `IHasAsyncSpecialMethods` | Async method support |
| `IHasCameraAutoModeMessenger` | Mobile Control camera interface |
| `IHasCodecRoomPresetsActionsMessenger` | Mobile Control presets interface |
| `IHasMeetingLockMessenger` | Mobile Control meeting lock interface |
| `IHasParticipantsMessenger` | Mobile Control participants interface |
| `IHasPhoneDialingMessenger` | Mobile Control phone dialing interface |
| `IHasSelfviewPositionMessenger` | Mobile Control selfview positioning |
| `IHasSelfviewSizeMessenger` | Mobile Control selfview sizing |
| `IHasZoomRoomLayoutsMessenger` | Mobile Control layout selection |

### Key Essentials Concepts

**Join Map**: SIMPL+ integration layer mapping device state to join numbers
**Messenger**: Mobile Control integration layer for remote app control
**Factory**: Plugin discovery and instantiation (implemented in `ZoomRoomFactory.cs`)
**Configuration**: SIMPL+ configuration object for instance setup

---

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Runtime | .NET | 8.0 |
| Language | C# | Latest (v12+) |
| SDK | PepperDash.ZoomRoom.Sdk | 1.1.0+ |
| Framework | PepperDashEssentials | 3.0.0+ |
| CI/CD | GitHub Actions | N/A |
| Release | semantic-release | 25.0.9 |
| Testing | xUnit (included in Essentials) | Latest |

---

## Communication Protocol

**Transport**: TCP Socket over LAN
**Zoom Room Address**: Configured in project configuration (typically 192.168.x.x)
**SDK Wrapper**: Native library (libzrcsdkwrapperpdt.so) embedded in plugin assembly
**Command Format**: Custom Zoom Room Protocol (defined by SDK)
**Response Format**: JSON event objects

---

## State Management

### Key State Variables

- **ConnectionState**: Connected / Disconnected / Connecting / Error
- **MeetingState**: NoMeeting / Joining / InMeeting / Leaving
- **AudioState**: Muted / Unmuted
- **VideoState**: On / Off
- **SharingState**: NotSharing / Sharing / ShareingOnly
- **Layout**: Gallery / Speaker / Focus / Custom
- **Participants**: List of participant objects with name, role, state
- **CameraList**: Available camera devices and their capabilities

### State Update Cycle

```
┌─────────────────┐
│  SDK Connected  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Poll/Subscribe │ (Configurable interval)
│  for State      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Receive Event  │
│  from SDK       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Update Device  │
│  State Object   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Fire Events    │
│  (OnStateChanged)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Update SIMPL   │
│  Feedback       │
└─────────────────┘
```

---

## Logging & Diagnostics

The plugin uses the Essentials logging framework:

**Log Levels**:
- `Critical` - Unrecoverable errors
- `Error` - Operation failed but recoverable
- `Warn` - Unexpected state, may cause issues
- `Info` - Normal operation milestones
- `Debug` - Detailed diagnostic info (development only)

**Key Log Sources**:
- `ZoomRoom` - Main device events
- `ZrcSdkController` - SDK communication details
- `ZoomRoomJoinMap` - SIMPL integration events

---

## Performance Characteristics

### Typical Timings

| Operation | Typical Time |
|-----------|-------------|
| Connect to hardware | 2-5 seconds |
| Mute/Unmute | 200-500ms |
| Start sharing | 1-3 seconds |
| Camera switch | 500ms-1s |
| Layout change | 200-400ms |
| Full state sync | 1-2 seconds |

### Resource Usage

- **Memory**: ~50-100 MB (depends on participant count)
- **CPU**: <5% idle, <15% during active control
- **Network**: ~1-5 Mbps during active meeting, <100kbps idle

---

## Deployment Model

```
GitHub Repository (epi-bic-zoomroom)
    │
    ├─ Push to csv-zoom-sandbox
    │
    ▼
GitHub Actions Build (Windows)
    │
    ├─ Run dotnet build
    ├─ Run dotnet test
    ├─ Generate .cplz artifact
    │
    ▼
GitHub Actions Release
    │
    ├─ Run semantic-release
    ├─ Create GitHub Release
    ├─ Publish .cplz as asset
    │
    ▼
PD Tools (Crestron)
    │
    ├─ Check for new versions
    ├─ Download .cplz from GitHub Release
    ├─ Deploy to CP4N
    │
    ▼
CP4N Processor
    │
    ├─ Extract and load plugin
    ├─ Initialize device
    ├─ Connect to Zoom Room hardware
```

---

## Next Steps

- **Understand the fork?** → Read [FORK-STRATEGY.md](FORK-STRATEGY.md)
- **Set up development?** → Read [QUICK-START.md](QUICK-START.md)
- **Build the project?** → Read [BUILDING.md](BUILDING.md)
- **Write code?** → Read [DEVELOPMENT.md](DEVELOPMENT.md)
- **Understand CI/CD?** → Read [CI-CD-PIPELINE.md](CI-CD-PIPELINE.md)

---

**Last Updated**: 2026-08-10
**Architecture Version**: 2.0 (Essentials v3, .NET 8)
