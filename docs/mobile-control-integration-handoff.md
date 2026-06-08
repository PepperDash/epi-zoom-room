# Zoom Room ↔ Mobile Control — integration handoff

**To:** Neil (owns `epi-beincourt-room` + `beincourt-pv2-react-app`)
**From:** Zoom Room EPI work (`epi-zoom-room`, branch `feature/v3-migration`)
**Status:** for discussion / planning. The `epi-zoom-room` changes are **committed and CI-published**
(`feature/v3-migration`, build 0/0, package `2.0.0-feature-v3-migration.3`) — pending hardware
validation of the Mobile Control surface. Nothing in your repos has been changed.

---

## 1. What changed on the EPI side (context)

`epi-zoom-room` now exposes the Zoom Room codec to Mobile Control as **device messengers** at
`/device/{zoomCodecKey}/...`. The codec implements ~23 capability interfaces; messengers come from two
places:

- **Core (auto-registered by Essentials)** for the standard interfaces the codec implements: meeting
  info, start meeting, password prompt, directory, **call controls (incl. incoming-call accept/reject)**,
  cameras, selfview, far-end content, schedule.
- **New per-interface messengers we added** (named `I{Interface}Messenger`, registered in the codec's
  `CreateMobileControlMessengers`): participants, participant pin/unpin, participant audio/video mute,
  meeting lock, meeting recording (+consent prompt), presentation-only meeting, camera auto-mode,
  selfview position, selfview size, phone dialing, Zoom layouts, wireless-share status. Plus a slim
  device-glue `ZoomRoomMessenger` (dial/invite, near-end camera mute + `videoUnmuteRequested`,
  end-meeting, directory root).

All of these are reachable at `/device/{zoomCodecKey}/...` and their status merges into
`state.devices[zoomCodecKey]` on the client.

---

## 2. The architecture — Essentials is the bridge (device-direct)

The important realization: **you do not need a custom `/room/{roomKey}/zoom/*` bridge.** Essentials'
core `MobileControlEssentialsRoomBridge` (auto-registered at `/room/{roomKey}` for every
`IEssentialsRoom`) already publishes the codec's **device key** to the React app — *if the room
implements `IHasVideoCodec`*:

```csharp
// Essentials core: MobileControlEssentialsRoomBridge.cs (~line 598)
if (room is IHasVideoCodec vcRoom && vcRoom.VideoCodec != null) {
    configuration.HasVideoConferencing = true;
    configuration.VideoCodecKey        = vcRoom.VideoCodec.Key;     // → roomState.configuration.videoCodecKey
    configuration.VideoCodecIsZoomRoom = type.Name == "ZoomRoom";   // → roomState.configuration.videoCodecIsZoomRoom
}
```

```
React app ── reads roomState.configuration.videoCodecKey ─► /device/{videoCodecKey}/*  +  s.devices[videoCodecKey]
                    ▲ Essentials publishes it for free                 ▲ ZoomRoom device (core + new messengers)
                    (only when the room implements IHasVideoCodec)
```

Your React app **already reads `roomState.configuration`** (`CamerasAndDisplays.tsx:98` →
`roomState.configuration.destinationList`), so codec discovery is essentially free once the room
advertises it.

---

## 3. What needs to happen — two edits in your repos

### A) `epi-beincourt-room` — make `BeincourtRoom` an `IHasVideoCodec` room
Today `BeincourtRoom` is `: EssentialsDevice, IEssentialsRoom`, so Essentials' `is IHasVideoCodec` test
is false and `videoCodecKey` is **never published**. Also note the existing `ZoomStateMachine` is an
**inert stub** — `eZoomTrigger.StartZoomMeeting/EndZoomMeeting` are declared in `.Permit(...)` but never
`Fire`d, and `BeincourtRoomMessenger` exposes only `zoomState` (no `zoom*` data, no `/zoom/*` actions).

**Do:**
- Add a config key for the Zoom codec device (e.g. `videoCodecKey`) to `BeincourtRoomConfig`.
- Resolve it in `BeincourtRoom` (`DeviceManager.GetDeviceForKey<VideoCodecBase>(...)`).
- Implement **`IHasVideoCodec`** (`: IHasInCallFeedback, IPrivacy`): expose `VideoCodec`, plus the
  in-call / privacy feedbacks the interface requires.
- (Optional) drive `zoomState` off the codec (e.g. `IHasMeetingInfo.MeetingInfoChanged` / call status)
  if you still want the room-level `zoomState`. Otherwise the app can derive it from device state.

**Do NOT** build `/zoom/*` actions or `zoom*` room state — that's not the Essentials pattern and would
duplicate the device messengers.

### B) `beincourt-pv2-react-app` — point Zoom controls at the device, not the room
Today the Zoom UI calls `sendMessage('/room/{roomKey}/zoom/*')` and renders from a hard-coded
`mockZoomState` (`ZoomControls.tsx`). Switch it to **device-direct**, the same pattern
`src/components/shared/hooks/useMicParticipants.ts` already uses:

```ts
const cfgKey = roomState?.configuration?.videoCodecKey;     // discovered from Essentials
const zoom   = useAppSelector(s => s.devices)[cfgKey];      // merged device status
sendMessage(`/device/${cfgKey}/selectLayout`, { value: layoutKey });
```

- Read `roomState.configuration.videoCodecKey` (+ `videoCodecIsZoomRoom`) for discovery — no hardcoding.
- Replace `mockZoomState` with `s.devices[videoCodecKey]`.
- Rewrite each `/room/{roomKey}/zoom/*` call per the **action mapping** below (path, name, payload, int ids).
- Add a participant adapter (codec `Participant` → app `ZoomParticipant`) per the **state mapping** below.
- Bug to fix while you're in there: `/zoom/incomingCall/accept` and `/zoom/incomingCall/decline`
  (`ZoomControls.tsx:224-225`) are missing the `/room/{roomKey}` (or now `/device/{key}`) prefix.

---

## 4. Action mapping (current app call → device messenger)

✅ covered  ⚠️ payload/name change  ❌ **capability gap** (needs more `epi-zoom-room` work — see §6)

| App action (today) | App payload | Device action (`/device/{videoCodecKey}/…`) | Device payload | |
|---|---|---|---|---|
| `zoom/endCall` | `{}` | `endMeeting` | `{}` | ✅ |
| `zoom/setLayout` | `{ layout }` | `selectLayout` | `{ value: layout }` | ⚠️ |
| `zoom/setSize` | `{ size }` | `setSelfviewSize` | `{ value: size }` | ⚠️ |
| `zoom/setPosition` | `{ position }` | `setSelfviewPosition` | `{ value: position }` | ⚠️ |
| `zoom/removeParticipant` | `{ id }` | `removeParticipant` | `{ value: <int> }` | ⚠️ id→int |
| `zoom/toggleParticipantAudioMute` | `{ id }` | `toggleParticipantAudioMute` | `{ value: <int> }` | ⚠️ |
| `zoom/toggleParticipantVideoMute` | `{ id }` | `toggleParticipantVideoMute` | `{ value: <int> }` | ⚠️ |
| `zoom/admitFromWaitingRoom` | `{ id }` | `admitParticipantFromWaitingRoom` | `{ value: <int> }` | ⚠️ name+id→int |
| `zoom/callContact` | `{ id }` | `invite` | `InvitableDirectoryContact` | ⚠️ resolve contact from directory |
| `/zoom/incomingCall/accept` / `decline` | `{}` | core call-controls accept/reject | — | ⚠️ + fix missing prefix |
| `zoom/startMeeting` (join scheduled) | `{ id }` | `joinScheduledMeeting` | `{ value: meetingId }` | ✅ **now added** |
| `zoom/joinMeeting` | `{ meetingId, passcode }` | `joinMeeting` | `{ meetingNumber, password? }` | ✅ **now added** (passcode-gated joins also raise the IPasswordPrompt flow) |
| `zoom/admitAllFromWaitingRoom` | `{}` | `admitAllFromWaitingRoom` | `{}` | ✅ **now added** |
| `zoom/removeFromWaitingRoom` | `{ id }` | `removeFromWaitingRoom` | `{ value: <int> }` | ✅ **now added** (expel semantics — flag if you meant something else) |
| `zoom/removeAllFromWaitingRoom` | `{}` | `removeAllFromWaitingRoom` | `{}` | ✅ **now added** |
| `zoom/addContact` / `editContact` / `deleteContact` | … | — | — | ❌ directory is read-only |
| `zoom/recallPreset` | `{ index }` | `recallPreset` | `{ value: <int> }` | ✅ **now added** (targets the selected camera) |
| `zoom/savePreset` | `{ index }` | `savePreset` | `{ index: <int>, description?: <string> }` | ✅ **now added** — `description` names the preset (surfaced in the list) |

### Device actions with no UI yet (new — wire up if wanted)
`pinParticipant`/`unpinParticipant`/`toggleParticipantPin` (`{ userId, screenIndex }`),
`setParticipantAsHost` (`{ value }`), `muteAllParticipants`, `cameraAutoModeOn|Off|Toggle`,
`toggleSelfviewPosition`/`toggleSelfviewSize`, `swapContentWithThumbnail`,
`toggleMeetingLock`/`lockMeeting`/`unlockMeeting`,
`toggleRecording`/`startRecording`/`stopRecording`/`recordPromptAcknowledge` (`{ value: <bool> }`),
`dialPresent`/`dialConvert`, `dialPhoneCall`/`endPhoneCall`/`sendDtmfToPhone` (`{ value: <string> }`),
schedule via the core messenger (`/schedule/...`).

---

## 5. State mapping (app `roomState.zoom*` → device status in `s.devices[videoCodecKey]`)

| App field (`BeincourtRoomState`) | Device status source | |
|---|---|---|
| `zoomParticipants` | `participants` (new `IHasParticipantsMessenger`) | ⚠️ shape differs (below) |
| `zoomMeetings` | `meetings` (core schedule messenger `/schedule/...`) | ⚠️ codec `Meeting` ≠ `{ id, time, title }` |
| `zoomState: Idle\|InMeeting` | derive from `meetingInfo` (core meeting-info messenger) | derived |
| `zoomContacts` | `currentDirectory` (`CodecDirectory`, core directory messenger) | ⚠️ tree → flat list |
| `zoomWaitingRoom` | `waitingRoom` on the participants messenger status (derived from silent-mode) | ✅ **now added** — `Participant[]` shape, map like `zoomParticipants` |
| `zoomPasscodeRequired` | core `IPasswordPrompt` messenger **event** (`passwordPrompt`: `message`, `lastAttemptWasIncorrect`, `loginAttemptFailed`, `loginAttemptCancelled`) | ✅ **now modeled** — event, not a status field (below) |
| `zoomPasscodeRequired**MeetingId**` | — | ❌ the `passwordPrompt` event carries no meeting id; UI remembers the id it just tried to join |
| `zoomIncomingCallerId` | core call-controls incoming-call data | needs mapping |
| *(new, available)* selfview position/size + options, `meetingIsLocked`, recording (`isRecording`, `recordConsentPromptIsVisible`), `cameraAutoModeIsOn`, `cameraIsMuted`, `numberOfScreens`, phone (`phoneOffHook`, `callerIdName/Number`), `shareInfo` | respective new messengers | |

### Passcode prompt is an event, not a status
There is **no `passwordPromptIsVisible` field** in device status to bind to. The codec fires a one-shot
`passwordPrompt` event (`EventType: "passwordPrompt"`) when a join needs a passcode or a prior attempt
was wrong. The app must hold `zoomPasscodeRequired` in **local state**:
- set `true` on receiving the `passwordPrompt` event (use `lastAttemptWasIncorrect` to show a retry error),
- submit with `sendMessage('/device/{videoCodecKey}/password', { value: passcode })`,
- clear to `false` on `loginAttemptCancelled`, on the next `meetingInfo` showing you're in the meeting, or
  after a successful submit.

Because the event carries no meeting id, remember the id you passed to `joinMeeting`/`joinScheduledMeeting`
locally and pair it with the prompt (covers `zoomPasscodeRequiredMeetingId`).

### Participant shape diff
App `ZoomParticipant` ← codec `Participant`:

| App `ZoomParticipant` | codec `Participant` | note |
|---|---|---|
| `id: string` | `UserId: int` | `UserId.toString()` |
| `name` | `Name` | |
| `role: "host"\|"coHost"\|"moderator"\|""` | `IsHost` / `IsCohost` | derive; no "moderator" source |
| `handRaised` | `HandIsRaisedFb` | |
| `audioMuted` | `AudioMuteFb` | |
| `videoMuted` | `VideoMuteFb` | |
| *(unused)* | `IsMyself`, `CanMuteVideo`, `CanUnmuteVideo`, `IsPinnedFb`, `ScreenIndexIsPinnedToFb` | extra device data (e.g. pin state) the UI could use |

---

## 6. Capability gaps — status on the `epi-zoom-room` side (ours)

**✅ Now closed (build 0/0; committed locally on `feature/v3-migration` — `e9d445a`, `11323bc`):**
- Join a **scheduled** meeting by id (`/joinScheduledMeeting`) and **join by number + passcode** (`/joinMeeting`).
- Waiting room: **admit-all** (`/admitAllFromWaitingRoom`), **remove** (`/removeFromWaitingRoom`), **remove-all** (`/removeAllFromWaitingRoom`).
- Distinct **`zoomWaitingRoom`** list — `waitingRoom` on the participants device status.
- **`zoomPasscodeRequired`** — already flows via the core `IPasswordPrompt` messenger (the device raises `PasswordRequired` on a passcode-gated join; the app prompts + `SubmitPassword`).

**✅ Now closed (also this round):**
- **Camera presets** (recall/save) — full stack implemented: SDK package
  `PepperDash.ZoomRoom.Sdk 1.1.0-feature-configurable-wrapper-path.22` (native ARM32 wrapper built by CI)
  + `ZoomRoom : IHasCodecRoomPresets`. App contract: `/device/{videoCodecKey}/recallPreset {value:int}`,
  `/device/{videoCodecKey}/savePreset {index:int, description?:string}`; preset list in the device status
  `presets` array (idx + name). **Max 3 presets per camera (fixed SDK limit), targets the selected camera.**
  Pending hardware validation.

**◻ Still open:**
- **Contact CRUD** (add/edit/delete) — SDK limitation; out of scope (confirmed).
- `zoomPasscodeRequired**MeetingId**` (which meeting needs the code) — the needs-password event doesn't carry it; simplest is for the UI to remember the id it just tried to join.

**Resolved decisions:**
- `removeFromWaitingRoom` = **expel/remove** the waiting user (confirmed).
- Camera presets target the **selected camera**; preset **names are surfaced** (status `presets[].name`)
  and editable via `/savePreset`'s `description` → `NameCameraPreset`. Cap is the fixed **3-preset, idx 0–2** SDK limit.

---

## 7. Checklist

**`epi-beincourt-room` (Neil):**
- [ ] Add `videoCodecKey` (codec device key) to `BeincourtRoomConfig`; resolve in `BeincourtRoom`.
- [ ] Implement `IHasVideoCodec` (+ `IHasInCallFeedback`, `IPrivacy`) exposing the Zoom codec.
- [ ] (Optional) drive/keep `zoomState` from codec events; otherwise let the app derive it.

**`beincourt-pv2-react-app` (Neil):**
- [ ] Read `roomState.configuration.videoCodecKey` for discovery.
- [ ] Replace `mockZoomState` with `s.devices[videoCodecKey]`.
- [ ] Rewrite `/room/{roomKey}/zoom/*` calls → `/device/{videoCodecKey}/*` per §4.
- [ ] Add the `Participant → ZoomParticipant` adapter per §5.
- [ ] Fix the missing prefix on `/zoom/incomingCall/accept|decline` (`ZoomControls.tsx:224-225`).

**`epi-zoom-room` (us):**
- [ ] Validate the new device messengers on hardware, then commit.
- [ ] Scope the §6 capability gaps the UI needs.
