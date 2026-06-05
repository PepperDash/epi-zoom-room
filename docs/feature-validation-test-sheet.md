# Zoom Room EPI — Feature Validation Test Sheet

Validates the SDK feature-exposure work wired so far on `feature/v3-migration`.

## Setup

- **Build/deploy** the current `feature/v3-migration` plugin (SDK ref `…wrapper-path.7`) to the test processor (`.116`) and confirm the room **pairs and connects**.
- **Device key:** these commands use `zoomRoom-1`. **Replace it with your actual device key** if different (check your config / the load log).
- **Console:** SSH to the processor and run the `devjson` commands below (copy the whole line). Each calls a public method on the device by name with a `params` array.
- **Reading results:**
  - **Volume / on-device UI:** watch the Zoom Room display / controller.
  - **Feedbacks (mute, self-view-on, first/last-page, CanRecord, etc.):** watch the linked bridge joins on your EISC/touchpanel, or the processor log.
- **Meeting state:** many features only do something **in an active meeting** — start/join a test meeting first where noted. Volume and the sharing-start commands work from idle.

> **IMPORTANT — "Method successfully called" ≠ the SDK accepted it.** That message only means the C# method didn't throw. As of build `ad599c2`, the controller **logs the SDK return code**: a non-zero/failed SDK result appears as a **`SDK call <Name> returned error code <n>` / `returned failure`** Warning in the log. So for each command, **also check the log** — no warning = SDK accepted it; a warning = the SDK rejected it (wrong state, feature unavailable, etc.). Set the device log level to Debug to also see `SDK call <Name> ok` lines.

### Required build
Deploy a build at **commit `ad599c2` or later** (SDK ref `…wrapper-path.7`). Earlier builds lack `LogParticipants` and the SDK return-code logging. (In the 2026-06-04 test run, `LogParticipants` returned "Unable to find method" because the deployed build predated it.)

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
- [ ] Device volume tracks **0% / 25% / 50% / 75% / 100%** respectively (the `.7` fix; previously 65535 only reached 39%).

```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteOn","params":[]}
```
- [ ] Output mutes (volume drops to 0); **MuteFeedback = true**.
```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteOff","params":[]}
```
- [ ] Output **restores to the previous level**; MuteFeedback = false.
```
devjson {"deviceKey":"zoomRoom-1","methodName":"MuteToggle","params":[]}
```
- [ ] Toggles mute on/off.
```
devjson {"deviceKey":"zoomRoom-1","methodName":"VolumeUp","params":[true]}
devjson {"deviceKey":"zoomRoom-1","methodName":"VolumeDown","params":[true]}
```
- [ ] Each call steps the volume up/down ~5%. (`params` must be `[true]` — it's the press flag.)
- [ ] On reconnect, **VolumeLevelFeedback** seeds to the room's current volume (set volume, reboot/redeploy, confirm feedback matches).

---

## 2. Self-view PiP (F3 / F4 / F5) — *in a meeting*

Use the **toggle** commands (no params) — most reliable:

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeToggle","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeOff","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfViewModeOn","params":[]}
```
- [ ] `Off` **hides** the self-view PiP; `On` restores it (at the last visible size); `Toggle` alternates. **SelfviewIsOnFeedback** matches.

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipSizeToggle","params":[]}
```
- [ ] Each call cycles size **Off → Size1 → Size2 → Size3 → Strip → Off**; PiP visibly resizes (Off hides it). **SelfviewPipSizeFeedback** updates.

```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipPositionToggle","params":[]}
```
- [ ] Each call moves the PiP through the corners **UpLeft → UpRight → DownRight → DownLeft**. **SelfviewPipPositionFeedback** updates.

Explicit set (optional — passes an object param; if your console can't deserialize it, use the toggles above):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipSizeSet","params":[{"command":"Size2","label":"Size 2"}]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SelfviewPipPositionSet","params":[{"command":"DownRight","label":"Lower Right"}]}
```
- [ ] PiP jumps directly to the requested size/position.

---

## 3. Layout (F6 + the C6 layout-style fix) — *in a meeting*

**Layout style** (this is the bug fix — Gallery/Speaker should actually switch the layout now, not just reorder tiles):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Gallery"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Speaker"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["Strip"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"SetLayout","params":["ShareAll"]}
```
- [ ] Gallery → gallery view; Speaker → speaker view; Strip → thumbnail/strip; ShareAll → content-only. (Before the fix these silently reordered tiles or no-op'd.)

**Paging** (needs more participants than fit on one page):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"LayoutTurnNextPage","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"LayoutTurnPreviousPage","params":[]}
```
- [ ] Gallery view pages forward/back. **LayoutViewIsOnFirstPage / LayoutViewIsOnLastPage** feedbacks update (true on first/last page) — driven by the SDK page-status event.

**Content/thumbnail swap** (needs shared content in the meeting):
```
devjson {"deviceKey":"zoomRoom-1","methodName":"SwapContentWithThumbnail","params":[]}
```
- [ ] Toggles between content-primary and video-primary on a single screen; **ContentSwappedWithThumbnailFeedback** flips each call.

---

## 4. Sharing-only meeting + HDMI share (F7 / F8)

**Sharing-only ("local presentation") meeting** — *from idle*:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":["Laptop"]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharingOnlyMeeting","params":["Ios"]}
```
- [ ] Room enters a sharing-only/local-presentation state; `Laptop`/`Ios` show the matching share-instruction overlay.
```
devjson {"deviceKey":"zoomRoom-1","methodName":"StartNormalMeetingFromSharingOnlyMeeting","params":[]}
```
- [ ] Converts the active local presentation into a normal Zoom meeting.

**HDMI ("black magic") share** — *needs an HDMI source connected to the room*:
```
devjson {"deviceKey":"zoomRoom-1","methodName":"StartSharing","params":[]}
devjson {"deviceKey":"zoomRoom-1","methodName":"StopSharing","params":[]}
```
- [ ] `StartSharing` begins sharing the HDMI input (shown locally); `StopSharing` ends it. **SharingContentIsOnFeedback** reflects state.

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

## 10. Local single-prominent layout toggle (N3)
```
devjson {"deviceKey":"zoomRoom-1","methodName":"LocalLayoutToggleSingleProminent","params":[]}
```
- [ ] Toggles between Speaker (one large tile) and Gallery; **LocalLayoutFeedback** tracks it.
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
