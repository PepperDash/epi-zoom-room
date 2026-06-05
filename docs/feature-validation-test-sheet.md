# Zoom Room EPI — Feature Validation Test Sheet

Validates the SDK feature-exposure work wired so far on `feature/v3-migration`.

> **Validation status (CP4N, firmware `.116`, SDK `…wrapper-path.20`, 2026-06-05):** the room **pairs, connects, and loads** (after the GLIBCXX toolchain fix). Sections **§1 volume, §2 self-view, §3 layout, §4 sharing-only meeting, §10 single-prominent** are **✅ SDK-accepted** (ran with no `returned failure` warning). Items checked below mean *"command executed and the SDK accepted it"*; where the box notes an on-screen/feedback effect, spot-check it on the room when convenient — the bare bench rig had **no camera, no HDMI source, no calendar, and was solo**, so §5–§9, §11, §12 (and HDMI share) still need an equipped room — see the remaining-commands list at the bottom.

## Setup

- **Build/deploy** the current `feature/v3-migration` plugin (SDK ref **`…wrapper-path.20`**) to the test processor (`.116`) and confirm the room **pairs and connects**.
  - **Required: SDK `.19` or later.** Earlier prereleases (`.8`–`.18`) were cross-compiled with a toolchain whose `libstdc++`/`glibc` are newer than 4-series firmware provides, so the native wrapper fails to `dlopen` (`GLIBCXX_3.4.29 not found`) and the device won't load. `.19`+ are built with the pinned GCC 9 toolchain (`GLIBCXX_3.4.21 / GLIBC_2.4`). See zoom-room-sdk-lib PR #4.
- **Device key:** these commands use `zoomRoom-1`. **Replace it with your actual device key** if different (check your config / the load log).
- **Console:** SSH to the processor and run the `devjson` commands below (copy the whole line). Each calls a public method on the device by name with a `params` array.
- **Reading results:**
  - **Volume / on-device UI:** watch the Zoom Room display / controller.
  - **Feedbacks (mute, self-view-on, first/last-page, CanRecord, etc.):** watch the linked bridge joins on your EISC/touchpanel, or the processor log.
- **Meeting state:** many features only do something **in an active meeting** — start/join a test meeting first where noted. Volume and the sharing-start commands work from idle.

> **IMPORTANT — "Method successfully called" ≠ the SDK accepted it.** That message only means the C# method didn't throw. As of build `ad599c2`, the controller **logs the SDK return code**: a non-zero/failed SDK result appears as a **`SDK call <Name> returned error code <n>` / `returned failure`** Warning in the log. So for each command, **also check the log** — no warning = SDK accepted it; a warning = the SDK rejected it (wrong state, feature unavailable, etc.). Set the device log level to Debug to also see `SDK call <Name> ok` lines.

### Required build
Deploy the current `feature/v3-migration` head (SDK ref **`…wrapper-path.20`**, GCC-9 native wrapper). This includes `LogParticipants`, the SDK return-code logging, and all N1–N4 features.

### Test conditions that matter (from first test pass)
- **Layout changes need MORE THAN ONE participant** to be visible — in a solo/instant meeting, Gallery/Speaker/Strip all look identical (one tile). Use a meeting with ≥2 video participants.
- **Sharing-only meeting** (`StartSharingOnlyMeeting`) is for starting **from idle** — calling it while already in a normal meeting will likely be rejected by the SDK (watch for the warning).
- **HDMI share** (`StartSharing` → `ShareBlackMagic`) needs an **HDMI source physically connected** to the room. On a bare test rig with no source, expect an SDK error/failure and no share feedback.
- Watching the **Zoom admin portal** may not reflect every on-device layout/PiP change the same way the room display does — the **log return code** is the reliable signal.

---

## 1. Room output volume (F11) — *no meeting required, just connected*

Range was corrected to 0–65535 ↔ SDK 0–255.

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[0]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[16384]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[32768]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[49152]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[65535]}
```
- [x] **SDK-accepted** at all 5 levels (CP4N 2026-06-05, no failure warning). *Spot-check the actual room %tracking (0/25/50/75/100) when convenient — the 0–255 scaling fix.*

```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteOn","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Confirm output mutes / MuteFeedback = true on the room.*
```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteOff","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Confirm output restores / MuteFeedback = false.*
```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteToggle","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Confirm it toggles mute on/off.*
```
devjson {"deviceKey":"zoomRoom-1","methodName":"VolumeUp","params":[true]}
devjson {"deviceKey":"zoomRoom-1","methodName":"VolumeDown","params":[true]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Confirm each call steps ~5%.* (`params` must be `[true]` — the press flag.)
- [ ] On reconnect, **VolumeLevelFeedback** seeds to the room's current volume (set volume, reboot/redeploy, confirm feedback matches). *(reboot test — not yet run)*

---

## 2. Self-view PiP (F3 / F4 / F5) — *in a meeting*

Use the **toggle** commands (no params) — most reliable:

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeOn","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05, no warning). *Confirm `Off` hides / `On` restores / `Toggle` alternates the PiP and SelfviewIsOnFeedback matches — best seen in a meeting.*

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipSizeToggle","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Confirm size cycles Off → Size1 → Size2 → Size3 → Strip → Off and SelfviewPipSizeFeedback updates — in a meeting.*

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipPositionToggle","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Confirm the PiP cycles corners and SelfviewPipPositionFeedback updates — in a meeting.*

Explicit set (passes an object param):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipSizeSet","params":[{"command":"Size2","label":"Size 2"}]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipPositionSet","params":[{"command":"DownRight","label":"Lower Right"}]}
```
- [x] **Not testable via `devjson`** — confirmed on CP4N these return *"Object must implement IConvertible"* (devjson can't deserialize the `{command,label}` object). The **toggle** commands above exercise the same code path; validate sizes/positions through those (or via the bridge/touchpanel).

---

## 3. Layout (F6 + the C6 layout-style fix) — *in a meeting*

**Layout style** (this is the bug fix — Gallery/Speaker should actually switch the layout now, not just reorder tiles):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Gallery"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Speaker"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Strip"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["ShareAll"]}
```
- [x] **SDK-accepted** for all four styles (CP4N 2026-06-05, no warning). *Visible switch (gallery/speaker/strip/content-only) needs a meeting with ≥2 video participants — confirm there.*

**Paging** (needs more participants than fit on one page):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"LayoutTurnNextPage","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"LayoutTurnPreviousPage","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Visible paging + LayoutViewIsOnFirstPage/LastPage feedbacks need more participants than fit one page — confirm in a populated meeting.*

**Content/thumbnail swap** (needs shared content in the meeting):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SwapContentWithThumbnail","params":[]}
```
- [x] **SDK-accepted** (CP4N 2026-06-05). *Visible swap needs shared content in the meeting; ContentSwappedWithThumbnailFeedback flips each call — confirm with content present.*

---

## 4. Sharing-only meeting + HDMI share (F7 / F8)

**Sharing-only ("local presentation") meeting** — *from idle*:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":["Laptop"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":["Ios"]}
```
- [x] **Validated on CP4N 2026-06-05** — SDK-accepted, and the room actually transitioned `ConnectingToMeeting → InMeeting`.
  > **Finding:** the `Laptop`/`Ios` param is the SDK's `LaunchSharingMeeting` *"init display state"* only — on 4-series firmware the overlay **always opens on Desktop** regardless of the param (the value reaches the SDK correctly as `IOS=2`; the SDK just doesn't render it at launch). To switch the live instruction, use **`ShowShareInstruction`** below.

**Switch the live instruction overlay** — *while in a sharing-only meeting* (this is the call that actually selects the tab):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"ShowShareInstruction","params":["Ios"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"ShowShareInstruction","params":["Laptop"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"DismissShareInstruction","params":[]}
```
- [x] **Validated on CP4N 2026-06-05** — `Ios` switches the room screen to the **iPhone/iPad** tab, `Laptop` back to **Desktop**, `DismissShareInstruction` hides the overlay. No SDK failure.
```
devjson {"deviceKey":"zoomRoom-1","methodName":"StartNormalMeetingFromSharingOnlyMeeting","params":[]}
```
- [x] **SDK-accepted** on CP4N 2026-06-05. *Confirm it converts the local presentation into a normal meeting.*

**HDMI ("black magic") share** — *needs an HDMI source connected to the room*:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharing","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StopSharing","params":[]}
```
- [ ] `StartSharing` begins sharing the HDMI input (shown locally); `StopSharing` ends it. **SharingContentIsOnFeedback** reflects state.
  > On the bare CP4N rig (2026-06-05) both returned `SDK call ShareBlackMagic/StopShare returned failure` — **expected with no HDMI source connected**. Retest on a room with an HDMI source.

---

## 5. Participant video mute (F2) — *in a meeting, as host, with another participant*

First dump the current participants and their userIds to the log:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"LogParticipants","params":[]}
```
- [ ] Log lists each participant: `userId=… name="…" host=… self=… audioMuted=… videoMuted=… handRaised=…`. Note the `userId` of the participant you want to target (not the one with `self=True` — that's the room).

Then exercise mute with that userId (replace `<userId>`):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"UnmuteVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"ToggleVideoForParticipant","params":[<userId>]}
```
- [ ] The target participant's video mutes / unmutes (host privilege required; the SDK may only allow mute, not force-unmute).
- [ ] Re-run `LogParticipants` to confirm the target's `videoMuted` flag changed.

> `LogParticipants` is also handy for the audio-mute (`MuteAudioForParticipant`) and pin (`PinUser`) commands if you want to spot-check those too.

---

## 6. CanRecord (F15) — *observe a feedback, not a command*

`MeetingInfo.CanRecord` is now driven by the SDK (was hardcoded `false`). It's a **feedback**, not a callable method.

- [ ] Join a meeting where this room **is allowed** to record → the **MeetingCanRecord** bridge join goes **true**.
- [ ] Join a meeting where recording is **not** permitted (or as a non-host where recording is host-only) → **MeetingCanRecord** stays **false**.
- [ ] Confirm it updates live if the host grants/revokes recording permission mid-meeting.

---

## 7. Bookings / schedule awareness (F14) — *needs a calendar-configured room*

`GetSchedule()` now calls the SDK (`ListMeeting`) instead of logging a no-op warning. Results arrive asynchronously via `MeetingListChanged` and populate `CodecSchedule.Meetings`. The schedule is **also auto-requested on connect**.

> Requires a Zoom Room with a **configured calendar** that has at least one scheduled meeting. Build `6035fce`+ (SDK ref `…wrapper-path.12`).

```
devjson {"deviceKey":"zoomRoom-1","methodName":"GetSchedule","params":[]}
```
- [ ] **No** `SDK call ListMeeting returned error code …` Warning in the log (set log level Debug to also see the `ok` line).
- [ ] Log shows `Schedule updated: N meeting(s) (result 0)` with the expected meeting count.
- [ ] `CodecSchedule.Meetings` populates and **MeetingsListHasChanged** fires (watch the schedule bridge joins / touchpanel).
- [ ] **ISO-8601 offsets resolve to correct local times** — spot-check a meeting's Start/End against the calendar (the most likely place for a bug).
- [ ] On **reconnect** (no command), the schedule auto-populates.
- [ ] A meeting with an unparseable/blank time is **silently dropped** (logged Debug `Skipping meeting …`), not surfaced with a default time.

---

## 8. Participant expel / assign-host (N1) — needs ≥2 in-meeting participants

Get userIds via `ZoomRoom.LogParticipants()` (console) first.
```
devjson {"deviceKey":"zoomRoom-1","methodName":"RemoveParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetParticipantAsHost","params":[<userId>]}
```
- [ ] Expelled participant actually leaves the meeting; **no** `SDK call ExpelUser returned error code …` Warning.
- [ ] Host role transfers; **no** `SDK call AssignHost returned error code …` Warning. (Must already be host to do either.)

## 9. Near-end camera PTZ + auto mode (N2a / N2b) — in a meeting, near-end camera

PTZ goes through the camera device's pan/tilt/zoom controls (bridge joins / touchpanel), not a direct `devjson` method. Auto mode:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeOn","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeToggle","params":[]}
```
- [ ] Near-end PTZ moves the **main** room camera; **no** `Camera command: unsupported …` or `SDK call ControlCamera returned error code …` Warning.
- [ ] AutoMode On engages speaker-tracking framing; Off returns to manual; **CameraAutoModeIsOnFeedback** flips with each.
- [ ] **No** `SDK call ChangeSmartCameraMode returned error code …` Warning.
  > On the bare CP4N rig (2026-06-05) all three returned `SDK call ChangeSmartCameraMode returned failure` — **expected: the rig has no camera** ("No local cameras reported by the SDK"). Retest on a camera-equipped room.

## 10. Local single-prominent layout toggle (N3)
```
devjson {"deviceKey":"zoomRoom-1","methodName":"LocalLayoutToggleSingleProminent","params":[]}
```
- [x] **SDK-accepted** on CP4N 2026-06-05 (no warning). *Visible Speaker↔Gallery toggle + LocalLayoutFeedback best confirmed in a ≥2-participant meeting.*
- [ ] `MinMaxLayoutToggle` is **expected to no-op** with a "no Zoom Room SDK equivalent" warning (by design).

## 11. Camera device list + SelectCamera (N2c) — needs ≥1 local camera

On connect, watch the log for `Added near-end camera id="…" name="…"` (Debug). Then:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelectCamera","params":["<deviceId>"]}
```
- [ ] Near-end cameras populate the `Cameras` list on connect with **real device IDs** (the `id` from the log); the USB-bridge (`HD-CONV-USB`) entry is skipped.
- [ ] **SelectedCamera/SelectedCameraFeedback** reflects the SDK's currently-selected camera at connect.
- [ ] `SelectCamera("<deviceId>")` switches the room's active camera; **no** `SDK call SetCurrentCamera returned error code …` Warning. An unknown key logs `SelectCamera: no camera with key …`.
- [ ] After selecting a specific camera, **N2a PTZ** and **N2b auto-mode** act on that camera (verify they target the selected device, not just the main one).
- [ ] **Known limitation (by design):** swapping the camera from the **Zoom UI** (not the plugin) won't update the feedback until the next reconnect or plugin-driven select — no live push is wired.

## 12. RejectCall — decline an incoming meeting invite (N4) — needs an inbound invite

Have another Zoom client/room **invite this room** so an incoming call is ringing, then:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"RejectCall","params":[]}
```
- [ ] The invite is **declined on the caller's side** (the caller sees the room declined), not just cleared locally; **no** `SDK call AnswerMeetingInvite returned error code …` Warning.
- [ ] With **no** pending invite, RejectCall logs `No pending meeting invite` (error -2) — expected, harmless.
- [ ] The local call status flips to **Disconnected** and the UI clears.
- [ ] (Accept path unchanged) `AcceptCall` still joins via meeting number — confirm it still works.

---

## Remaining to validate on a fully-equipped room

The 2026-06-05 CP4N pass cleared everything the bench rig could exercise. The items below still need a **camera-equipped, calendar-linked Zoom Room in a ≥2-participant meeting**, plus an HDMI source and an inbound invite. Grouped by what each needs:

**A ≥2-participant meeting + this room as host** (get userIds from `LogParticipants` first):
```text
devjson {"deviceKey":"zoomRoom-1","methodName":"LogParticipants","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"UnmuteVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"ToggleVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"RemoveParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetParticipantAsHost","params":[<userId>]}
```
*Also re-confirm the **visible** effect of the already-SDK-accepted layout/self-view commands here (switching, paging, swap, single-prominent, PiP size/position).*

**A local camera attached:**
```text
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeOn","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelectCamera","params":["<deviceId>"]}
```
*Watch the connect log for `Added near-end camera id="…"` to get the real `<deviceId>`. Near-end PTZ runs through bridge/touchpanel joins, not devjson.*

**An HDMI source connected to the room:**
```text
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharing","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StopSharing","params":[]}
```

**A configured calendar with a scheduled meeting:**
```text
devjson {"deviceKey":"zoomRoom-1","methodName":"GetSchedule","params":[]}
```
*Expect `Schedule updated: N meeting(s) (result 0)` and populated `CodecSchedule.Meetings` (the bench rig returned `result 5 / 0 meetings` = no calendar).*

**An inbound meeting invite ringing the room:**
```text
devjson {"deviceKey":"zoomRoom-1","methodName":"RejectCall","params":[]}
```

**Observe-only feedbacks (no command):**
- **CanRecord (§6)** — join a recording-permitted vs. not-permitted meeting, watch `MeetingCanRecord`.
- **Volume seed-on-reconnect (§1)** — set volume, reboot/redeploy, confirm `VolumeLevelFeedback` matches.

---

## Not yet testable via `devjson` (wired but incomplete / by design)

- _(none — all of N1–N4 is wired; the items below are deferred enhancements, not stubs)_
- **N2c live push:** external (Zoom-UI) camera swaps don't update feedback until reconnect/select (no `ISettingServiceSink`).
- **N4 invite-response event:** the SDK's `OnAnswerMeetingInviteResponse` isn't surfaced to the host (empty native stub); feedback is the `Rc` log + local status.

> Now implemented (add/extend test steps as hardware allows): far-end camera discovery (R-A), PSTN dial-out (R-B), invite-by-contactID (R-C), directory/contacts (R-D), bookings/schedule (R-E, §7 above), participant expel/assign-host (N1, §8), near-end PTZ + auto mode (N2a/N2b, §9), single-prominent layout (N3, §10), camera list + SelectCamera (N2c, §11), RejectCall/decline invite (N4, §12).

---

## Appendix — All `devjson` commands (one-click copy)

Every command from the sheet above, in section order. Replace `<userId>` / `<deviceId>` placeholders before running, and adjust `deviceKey` if your config differs.

```text
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[0]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[16384]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[32768]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[49152]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetVolume","params":[65535]}
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteOn","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"VolumeUp","params":[true]}
devjson {"deviceKey":"zoomRoom-1","methodName":"VolumeDown","params":[true]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeOn","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipSizeToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipPositionToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipSizeSet","params":[{"command":"Size2","label":"Size 2"}]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipPositionSet","params":[{"command":"DownRight","label":"Lower Right"}]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Gallery"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Speaker"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Strip"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["ShareAll"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"LayoutTurnNextPage","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"LayoutTurnPreviousPage","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SwapContentWithThumbnail","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":["Laptop"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":["Ios"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"ShowShareInstruction","params":["Ios"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"ShowShareInstruction","params":["Laptop"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"DismissShareInstruction","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartNormalMeetingFromSharingOnlyMeeting","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharing","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StopSharing","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"LogParticipants","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"UnmuteVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"ToggleVideoForParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"GetSchedule","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"RemoveParticipant","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetParticipantAsHost","params":[<userId>]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeOn","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"CameraAutoModeToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"LocalLayoutToggleSingleProminent","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelectCamera","params":["<deviceId>"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"RejectCall","params":[]}
```

---

## Notes / issues found

> Record anything that doesn't match expected here (command, what happened, what you expected). The volume **range** is already confirmed (0–255). Most likely places for surprises: layout-style int mapping (Strip/ShareAll), self-view size/position enum mapping, and the share-instruction display states.
