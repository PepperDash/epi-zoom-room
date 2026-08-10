# [2.0.0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/compare/v1.0.0...v2.0.0) (2026-08-10)


### Bug Fixes

* add build infrastructure to produce NuGet package and CPLZ artifact ([e77d430](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e77d430b919a943eeb76b8ed0acc4be49f70e3bd))
* address Copilot PR review feedback ([85f1d95](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/85f1d955aa647c38bacfbea0bb48af810b21e91d))
* **build:** bump SDK to .24, add missing Essentials.Core usings ([cb1b4e5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cb1b4e5aa8fea3e95ee74fe0b9638167286c269f))
* bump PepperDashEssentials to 3.0.0-dev-v3-routing.12 ([e1da763](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e1da7638ce126ee246ce9a535b435dbe3263649a))
* bump PepperDashEssentials to 3.0.0-dev-v3-routing.13 ([0396083](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/039608369ade50eac8359a7d775e1a60c8b4fad9))
* clarify DigitalSerial 221-224 layout join descriptions ([fd4de8c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/fd4de8c97987690c11dbd138c4b502c6d958697e))
* **code-review:** C1/C2/C5/B1/B2 remediation items ([cd4ebe0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cd4ebe0871edaecac6e76d11a551e7f170bc7bc8))
* demote directory and schedule update logs to Debug ([047c7f2](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/047c7f28677dcf716db7c9dfdc853bc57fd156b1))
* **logging:** demote phonebook download complete to debug level ([fef4741](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/fef474100e8a80a3d513e90cca76080a82acc8da))
* **nuget:** document PACKAGES_PAT requirement for CI GitHub Packages auth ([78dedc7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/78dedc70cb28c8149eca7d556c41ce7187994371))
* **p0/p1:** SetIsReady, consent latch, Output3, inverted bool, stale password ([032510d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/032510da5e1a4070d5943679dae2da4b3a5fd36b)), closes [#1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/1) [#8](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/8) [#11](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/11) [#15](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/15)
* **p0:** add auto-reconnect with backoff on disconnect ([#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)) ([733c4d6](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/733c4d6a121ef04c32f3b09054d4ab4eef0eaac3))
* **p0:** camera preset off-by-one on SIMPL bridge ([#5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/5)) ([dc144ca](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/dc144ca7c549098a052b772b2c2c8a221ca40ad7))
* **p0:** ComputeAvailableLayouts guaranteed NRE ([#2](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/2)) ([f33cc49](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f33cc493f355dc95c2cdfed861bcaca97b73537b))
* **p0:** paginate phonebook beyond 50 contacts ([#3](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/3)) ([e5e6a93](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e5e6a933a4e84a9a7edac2be4075a4e0886e28d3))
* **p0:** participant roster race + pin dictionary race ([#9](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/9), [#10](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/10)) ([92da1bd](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/92da1bd2d01f30bf70cab2c17875d9557b1f793c))
* **p0:** phone feedbacks backed by SDK SIP call state ([#7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/7)) ([dd44754](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/dd44754e01376cb0bb4aadabe77d786fad8f9af0))
* **p0:** replace orphaned zStatus/zConfig reads with live-backed values ([#13](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/13)) ([e410cbc](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e410cbc0f123dee0955d53e1eee1690c3923c258))
* **p0:** unify meeting teardown into ResetMeetingState() ([#12](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/12)) ([1840946](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/184094606aef4c0e7b3e55ce312a50597635b4cc))
* **p0:** wire AirPlay/HDMI share instructions surface ([#6](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/6)) ([b556c53](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/b556c539a708e78ed98bceae8ed5ec81345a6ceb))
* **p1:** poll bookings every 5 min to catch calendar changes ([#14](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/14)) ([cd014cf](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cd014cfd644d9fe0c5187d39d3d1f686efb2c224))
* **p1:** remove duplicate roster publish via ParticipantCountChanged ([#17](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/17)) ([648122a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/648122add700e6fb14b5f9236f49da5880797493))
* **participants:** apply co-host (and other role) changes re-sent via UserJoined ([243db1c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/243db1c8043a5b58e975aa1a24ee6efc3d9e52ed))
* **phase2:** address code review items P1-1/2/6, P2-1/2/4/5, A1/A2/A3/C4 ([4645829](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/4645829e242bcc8ffe55e903df6226db1bb97d01))
* reference Essentials via NuGet packages instead of local project references ([c8b6ec4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/c8b6ec498fd6fb89086cae10169a0df5649c7b6e))
* remove stray SelfviewPipSizeToggle@231 join-map entry ([942d7cf](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/942d7cfe45150c1eeb840034c90dffa56c7640fa)), closes [#1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/1) [#714](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/714)
* rename MeetWithImUsers -> MeetWithIMUsers ([124f67c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/124f67cbda1e7d399d3cfd660fba8214a70ed4aa))
* resolve digital 206 output collision; move CanSwap FB to join 205 ([d0a9169](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/d0a916988b52dd6298d9795e0513aee8e98b5476))
* **zoom-room:** code-review remediation (camera race, readiness gate, init check, layout state) ([791fe65](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/791fe65a1995f518d7d3cdc9d5015f375406d123))
* **zoom-room:** correct layout-style call + wire far-end camera control ([a71ebc3](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/a71ebc39ffca16be2d250a30db71523741e92eb1))
* **zoom-room:** speaker volume scale is 0-255, not 0-100 ([ff854c0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ff854c066a78cb8fcf7d67b2ca995c6b17c8bb07))
* **zoom:** derive host state from roster, not just the HostChanged event ([0f3d197](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/0f3d197fce6204b945af2fac49297f7f5e30f27d))
* **zoom:** embed native ZRC wrapper in plugin assembly ([53d6444](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/53d6444bcf6382ed7169b5d587ce0b3395a50ccd))
* **zoom:** pin-toggle state tracking + LogMeetingInfo; document mute semantics ([7b1a9b8](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7b1a9b84e12ef7bbf34e3d7f36d00b55fc03e2a0))
* **zoom:** rename {Key} log placeholders to avoid IKeyed enricher collision ([9c36882](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/9c36882c641f383b7d88c23360a61b92ce69b697))
* **zoom:** stage native wrapper to writable dir via SetLibraryPath ([29fc7ef](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/29fc7ef54b763ee9c20584328b2aa817f7d7df4d))
* **zoom:** surface incoming meeting invite as a ringing ActiveCall (N4) ([b8981e5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/b8981e5ae762fd1621b82e972e321df61d54076b))
* **zoom:** surface SDK build failures + fetch data only when Connected; SDK .19 ([00c83f1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/00c83f194949ea764c5e1f92118ed2d423a2c0ef)), closes [#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)


### chore

* mark v3 migration as breaking change for semver ([411ba8a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/411ba8a505c89b6a094865f439f3f80ec25f300e))


### Features

* add GetMeetingStatus method to IZoomRoomController and implement in ZrcSdkController; update PepperDash.ZoomRoom.Sdk package version ([981c6db](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/981c6dbd3d4af005ba347d70fb37cd390f943709))
* add GitHub Actions workflow for building Essentials Plugin ([ce25a23](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ce25a23c6ff05a55a6fddc8752904fe0b92e9edf))
* add multiple USB camera input ports and rename existing input port in ZoomRoom ([2039f45](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/2039f459c9d646d14e0c4e97ec745db2fc962115))
* add ScreenLayoutStatus event and update layout handling in ZoomRoom ([df340e1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/df340e1669024b6c8bdc83411e1bfe7ea5ba4fa5))
* **console:** add LogVolume shim for console-based volume seed test ([9cdd56f](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/9cdd56fe8f4f499e5e427fb1d50312f9cab6617f))
* **directory:** wire R-C invite-by-contact + R-D directory subscription ([f99cc0f](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f99cc0fae8520de9a1c7fc7f6f7f0fd395cb14a7))
* enable assembly info generation in project file ([7b9d839](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7b9d83941d72c032982f0fbf1aafc5e8318b243e))
* **joinmap:** add PhonebookGet trigger; fix mislabeled/duplicated joins ([3fdc9c0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/3fdc9c0e0ae2a2ce134b86b5653ca8e679b72774))
* **layouts:** enhance layout options handling and update state representation ([96e94c8](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/96e94c83833b8d2ff9dac12503c1ad48f0f66282))
* migrate to Essentials v3 / net8 (Phase 1) ([0477454](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/04774540c93255595dc3484815fc00876a4f0ab4))
* **mobilecontrol:** expose Zoom Room to Mobile Control via per-interface messengers ([e9d445a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e9d445addeb5028d0806b8841a5650fc3fed69aa))
* override Dial method in ZoomRoom and update PepperDashEssentials package version ([bd882eb](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/bd882ebb6281c26eaf818436a2e1c55f2d5f2f94))
* **presets:** expose camera presets (recall/save) via IHasCodecRoomPresets ([11323bc](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/11323bceef5e7e5a6096571cff8e9ed903b6f029))
* remove CodecOsdIn input port and associated OSD source setup from ZoomRoom ([d6095cd](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/d6095cd76081e4c26109d445f396734036aca6e9))
* remove CustomActivate method and streamline feedback handling in multiple messengers ([03bbc6a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/03bbc6a15f2c13c0bff2844da74f57bb2089ff6c))
* replace SSH/ZRCLI transport with ZRC SDK controller pairing ([a367098](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/a367098330a2875e697a2ec7a7fcc7f9f923859a))
* **routing:** add RoutingInputPort for HDMI input in ZoomRoom ([fd27c62](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/fd27c62632cf7ac32c7ebf2671da35ed6e117fea))
* **routing:** implement IRoutingSinkWithFeedback and ICurrentSources interfaces in ZoomRoom ([99f62f0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/99f62f02dbe2b143a9e4ba6978ca66d9f6a1baee))
* **schedule:** expose bookings/schedule awareness via ListMeeting ([6035fce](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/6035fcefec03ca9783550812e95b8cebbdfcd00b))
* update PepperDashEssentials package version to 3.0.0-dev-v3-routing.32 ([da2621b](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/da2621bce415c817b087ca5a84c099a7aa002a9f))
* update PepperDashEssentials package version to 3.0.0-dev-v3-routing.50 ([028927e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/028927e1fe695d61745b7e6c1951064f8f26faf2))
* update StartSharing method to use StartSharingOnlyMeeting for HDMI source sharing ([4e3d1b1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/4e3d1b1a19d1e97a6c2fd02b20c7ea19c7e31cbd))
* Update ZoomRoomJoinMap and zConfiguration for new layout options ([ac0ecb5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ac0ecb5da792c4ab47caca7f6729be3039422a2a))
* **zoom-room:** add LogParticipants console helper for devjson testing ([49dd43e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/49dd43edb93087efd556de3c66c1d38eed0dfd99))
* **zoom-room:** far-end camera discovery + PSTN dial-out ([32d1c06](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/32d1c0692f962c11ea41a6ccd1a734717ab454d3))
* **zoom-room:** log SDK return codes for action commands (remote diagnosis) ([ad599c2](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ad599c20b3a3a67101537535b3fc4bab9603a3b7))
* **zoom-room:** populate MeetingInfo.CanRecord from SDK recording info ([6c2195c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/6c2195cbfbe1f5d083dab803a58ce8bb126762de))
* **zoom-room:** SIP phone dialing/DTMF + switch to PepperDash package feed ([9908105](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/9908105ac9971bf417da16e838de5cfa52b8e786))
* **zoom-room:** wire content/thumbnail swap, HDMI share, and page-status feedback ([166361d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/166361df72a6bab0d06c1a7f7a179b7dc738a3aa))
* **zoom-room:** wire participant video mute to SDK MuteUserVideo ([812170b](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/812170b121876f1f4dfd10994387886652196454))
* **zoom-room:** wire room output volume (SetVolume/Up/Down/Mute) to SDK ([efb30b7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/efb30b7fefecf11c07c0cbe1d79d86576377d438))
* **zoom-room:** wire self-view, layout paging, and sharing-only meeting to SDK ([5646a63](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/5646a631acae091660abc7b707554fe0b78587d0))
* **zoom:** add EndMeetingForAll device method ([0e256e1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/0e256e1dd42884f377f2beecbb5d45d7aee2fcd9))
* **zoom:** add forceRepairZoom console command for code rotation ([1aabf0d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/1aabf0d322edf80ad514e0d508b1224990392e99))
* **zoom:** add LogDirectory / InviteContactById console test helpers (R-C/R-D) ([cdf7f8c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cdf7f8c37f9edecb0a46a3e01f139d2a6801ca01))
* **zoom:** chronological schedule, enriched incoming-call details, waiting-room helpers ([159fe24](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/159fe24d77602e7f0d29ac4a55aae75cba7df424)), closes [#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)
* **zoom:** expose ShowShareInstruction/DismissShareInstruction (F8) ([f5ad95e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f5ad95e3d7c3071d96ef4b086ad3d04308d7c36b))
* **zoom:** guard participant host-controls against invalid/self targets ([1650801](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/1650801637a356610991d429920d68f57105fc42))
* **zoom:** map participant Cohost role (Essentials .7) ([87ec69e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/87ec69e1537a351ac647ea7975f707657e17cb56)), closes [#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)
* **zoom:** un-stub camera, participant, and layout features ([f34a51d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f34a51d057be964ae3a9b6e215dbf0941f43a475))
* **zoom:** wire camera selection + device list (N2c) ([aab394f](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/aab394fa31ce0dcce6d34b0fed65c4897b955497))
* **zoom:** wire RejectCall to decline meeting invites (N4) ([59715b7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/59715b79136fe98e50e8a259b170735480e2e3b2))


### Performance Improvements

* **p2:** debounce roster MC posts with 250 ms CTimer ([#31](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/31)) ([7c39c7c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7c39c7cac9887cbb43057da11950e207431122f3))
* **p2:** defer directory rebuild + publish to final phonebook page ([#30](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/30)) ([07a2824](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/07a28249a2f509114a30dd4d6a7b391959e99115))
* **p2:** skip far-end camera reconciliation when controllable set is unchanged ([#32](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/32)) ([93357a5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/93357a569af0217a2878d7ce7db6b17f1d980b32))
* **p2:** skip MeetingInfo allocation + event when fields unchanged ([#33](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/33)) ([7b3e677](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7b3e677481577950ff14923976d661cbd8bceb9d))


### BREAKING CHANGES

* **zoom-room:** the public CameraSelected event signature changed from
EventHandler<CameraSelectedEventArgs> to
EventHandler<CameraSelectedEventArgs<IHasCameraControls>>. Subscribers compiled
against the old non-generic signature must update their handler. (EPI-8;
pre-existing on this branch, documented here.)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
* drops 3-Series / .NET 3.5 CF support. Requires Essentials v3 and a 4-Series processor.

# [2.0.0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/compare/v1.0.0...v2.0.0) (2026-08-10)


### Bug Fixes

* add build infrastructure to produce NuGet package and CPLZ artifact ([e77d430](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e77d430b919a943eeb76b8ed0acc4be49f70e3bd))
* address Copilot PR review feedback ([85f1d95](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/85f1d955aa647c38bacfbea0bb48af810b21e91d))
* **build:** bump SDK to .24, add missing Essentials.Core usings ([cb1b4e5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cb1b4e5aa8fea3e95ee74fe0b9638167286c269f))
* bump PepperDashEssentials to 3.0.0-dev-v3-routing.12 ([e1da763](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e1da7638ce126ee246ce9a535b435dbe3263649a))
* bump PepperDashEssentials to 3.0.0-dev-v3-routing.13 ([0396083](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/039608369ade50eac8359a7d775e1a60c8b4fad9))
* clarify DigitalSerial 221-224 layout join descriptions ([fd4de8c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/fd4de8c97987690c11dbd138c4b502c6d958697e))
* **code-review:** C1/C2/C5/B1/B2 remediation items ([cd4ebe0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cd4ebe0871edaecac6e76d11a551e7f170bc7bc8))
* demote directory and schedule update logs to Debug ([047c7f2](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/047c7f28677dcf716db7c9dfdc853bc57fd156b1))
* **logging:** demote phonebook download complete to debug level ([fef4741](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/fef474100e8a80a3d513e90cca76080a82acc8da))
* **nuget:** document PACKAGES_PAT requirement for CI GitHub Packages auth ([78dedc7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/78dedc70cb28c8149eca7d556c41ce7187994371))
* **p0/p1:** SetIsReady, consent latch, Output3, inverted bool, stale password ([032510d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/032510da5e1a4070d5943679dae2da4b3a5fd36b)), closes [#1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/1) [#8](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/8) [#11](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/11) [#15](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/15)
* **p0:** add auto-reconnect with backoff on disconnect ([#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)) ([733c4d6](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/733c4d6a121ef04c32f3b09054d4ab4eef0eaac3))
* **p0:** camera preset off-by-one on SIMPL bridge ([#5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/5)) ([dc144ca](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/dc144ca7c549098a052b772b2c2c8a221ca40ad7))
* **p0:** ComputeAvailableLayouts guaranteed NRE ([#2](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/2)) ([f33cc49](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f33cc493f355dc95c2cdfed861bcaca97b73537b))
* **p0:** paginate phonebook beyond 50 contacts ([#3](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/3)) ([e5e6a93](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e5e6a933a4e84a9a7edac2be4075a4e0886e28d3))
* **p0:** participant roster race + pin dictionary race ([#9](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/9), [#10](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/10)) ([92da1bd](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/92da1bd2d01f30bf70cab2c17875d9557b1f793c))
* **p0:** phone feedbacks backed by SDK SIP call state ([#7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/7)) ([dd44754](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/dd44754e01376cb0bb4aadabe77d786fad8f9af0))
* **p0:** replace orphaned zStatus/zConfig reads with live-backed values ([#13](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/13)) ([e410cbc](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e410cbc0f123dee0955d53e1eee1690c3923c258))
* **p0:** unify meeting teardown into ResetMeetingState() ([#12](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/12)) ([1840946](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/184094606aef4c0e7b3e55ce312a50597635b4cc))
* **p0:** wire AirPlay/HDMI share instructions surface ([#6](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/6)) ([b556c53](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/b556c539a708e78ed98bceae8ed5ec81345a6ceb))
* **p1:** poll bookings every 5 min to catch calendar changes ([#14](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/14)) ([cd014cf](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cd014cfd644d9fe0c5187d39d3d1f686efb2c224))
* **p1:** remove duplicate roster publish via ParticipantCountChanged ([#17](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/17)) ([648122a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/648122add700e6fb14b5f9236f49da5880797493))
* **participants:** apply co-host (and other role) changes re-sent via UserJoined ([243db1c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/243db1c8043a5b58e975aa1a24ee6efc3d9e52ed))
* **phase2:** address code review items P1-1/2/6, P2-1/2/4/5, A1/A2/A3/C4 ([4645829](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/4645829e242bcc8ffe55e903df6226db1bb97d01))
* reference Essentials via NuGet packages instead of local project references ([c8b6ec4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/c8b6ec498fd6fb89086cae10169a0df5649c7b6e))
* remove stray SelfviewPipSizeToggle@231 join-map entry ([942d7cf](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/942d7cfe45150c1eeb840034c90dffa56c7640fa)), closes [#1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/1) [#714](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/714)
* rename MeetWithImUsers -> MeetWithIMUsers ([124f67c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/124f67cbda1e7d399d3cfd660fba8214a70ed4aa))
* resolve digital 206 output collision; move CanSwap FB to join 205 ([d0a9169](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/d0a916988b52dd6298d9795e0513aee8e98b5476))
* **zoom-room:** code-review remediation (camera race, readiness gate, init check, layout state) ([791fe65](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/791fe65a1995f518d7d3cdc9d5015f375406d123))
* **zoom-room:** correct layout-style call + wire far-end camera control ([a71ebc3](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/a71ebc39ffca16be2d250a30db71523741e92eb1))
* **zoom-room:** speaker volume scale is 0-255, not 0-100 ([ff854c0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ff854c066a78cb8fcf7d67b2ca995c6b17c8bb07))
* **zoom:** derive host state from roster, not just the HostChanged event ([0f3d197](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/0f3d197fce6204b945af2fac49297f7f5e30f27d))
* **zoom:** embed native ZRC wrapper in plugin assembly ([53d6444](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/53d6444bcf6382ed7169b5d587ce0b3395a50ccd))
* **zoom:** pin-toggle state tracking + LogMeetingInfo; document mute semantics ([7b1a9b8](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7b1a9b84e12ef7bbf34e3d7f36d00b55fc03e2a0))
* **zoom:** rename {Key} log placeholders to avoid IKeyed enricher collision ([9c36882](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/9c36882c641f383b7d88c23360a61b92ce69b697))
* **zoom:** stage native wrapper to writable dir via SetLibraryPath ([29fc7ef](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/29fc7ef54b763ee9c20584328b2aa817f7d7df4d))
* **zoom:** surface incoming meeting invite as a ringing ActiveCall (N4) ([b8981e5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/b8981e5ae762fd1621b82e972e321df61d54076b))
* **zoom:** surface SDK build failures + fetch data only when Connected; SDK .19 ([00c83f1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/00c83f194949ea764c5e1f92118ed2d423a2c0ef)), closes [#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)


### chore

* mark v3 migration as breaking change for semver ([411ba8a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/411ba8a505c89b6a094865f439f3f80ec25f300e))


### Features

* add GetMeetingStatus method to IZoomRoomController and implement in ZrcSdkController; update PepperDash.ZoomRoom.Sdk package version ([981c6db](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/981c6dbd3d4af005ba347d70fb37cd390f943709))
* add GitHub Actions workflow for building Essentials Plugin ([ce25a23](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ce25a23c6ff05a55a6fddc8752904fe0b92e9edf))
* add multiple USB camera input ports and rename existing input port in ZoomRoom ([2039f45](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/2039f459c9d646d14e0c4e97ec745db2fc962115))
* add ScreenLayoutStatus event and update layout handling in ZoomRoom ([df340e1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/df340e1669024b6c8bdc83411e1bfe7ea5ba4fa5))
* **console:** add LogVolume shim for console-based volume seed test ([9cdd56f](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/9cdd56fe8f4f499e5e427fb1d50312f9cab6617f))
* **directory:** wire R-C invite-by-contact + R-D directory subscription ([f99cc0f](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f99cc0fae8520de9a1c7fc7f6f7f0fd395cb14a7))
* enable assembly info generation in project file ([7b9d839](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7b9d83941d72c032982f0fbf1aafc5e8318b243e))
* **joinmap:** add PhonebookGet trigger; fix mislabeled/duplicated joins ([3fdc9c0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/3fdc9c0e0ae2a2ce134b86b5653ca8e679b72774))
* **layouts:** enhance layout options handling and update state representation ([96e94c8](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/96e94c83833b8d2ff9dac12503c1ad48f0f66282))
* migrate to Essentials v3 / net8 (Phase 1) ([0477454](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/04774540c93255595dc3484815fc00876a4f0ab4))
* **mobilecontrol:** expose Zoom Room to Mobile Control via per-interface messengers ([e9d445a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/e9d445addeb5028d0806b8841a5650fc3fed69aa))
* override Dial method in ZoomRoom and update PepperDashEssentials package version ([bd882eb](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/bd882ebb6281c26eaf818436a2e1c55f2d5f2f94))
* **presets:** expose camera presets (recall/save) via IHasCodecRoomPresets ([11323bc](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/11323bceef5e7e5a6096571cff8e9ed903b6f029))
* remove CodecOsdIn input port and associated OSD source setup from ZoomRoom ([d6095cd](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/d6095cd76081e4c26109d445f396734036aca6e9))
* remove CustomActivate method and streamline feedback handling in multiple messengers ([03bbc6a](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/03bbc6a15f2c13c0bff2844da74f57bb2089ff6c))
* replace SSH/ZRCLI transport with ZRC SDK controller pairing ([a367098](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/a367098330a2875e697a2ec7a7fcc7f9f923859a))
* **routing:** add RoutingInputPort for HDMI input in ZoomRoom ([fd27c62](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/fd27c62632cf7ac32c7ebf2671da35ed6e117fea))
* **routing:** implement IRoutingSinkWithFeedback and ICurrentSources interfaces in ZoomRoom ([99f62f0](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/99f62f02dbe2b143a9e4ba6978ca66d9f6a1baee))
* **schedule:** expose bookings/schedule awareness via ListMeeting ([6035fce](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/6035fcefec03ca9783550812e95b8cebbdfcd00b))
* update PepperDashEssentials package version to 3.0.0-dev-v3-routing.32 ([da2621b](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/da2621bce415c817b087ca5a84c099a7aa002a9f))
* update PepperDashEssentials package version to 3.0.0-dev-v3-routing.50 ([028927e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/028927e1fe695d61745b7e6c1951064f8f26faf2))
* update StartSharing method to use StartSharingOnlyMeeting for HDMI source sharing ([4e3d1b1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/4e3d1b1a19d1e97a6c2fd02b20c7ea19c7e31cbd))
* Update ZoomRoomJoinMap and zConfiguration for new layout options ([ac0ecb5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ac0ecb5da792c4ab47caca7f6729be3039422a2a))
* **zoom-room:** add LogParticipants console helper for devjson testing ([49dd43e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/49dd43edb93087efd556de3c66c1d38eed0dfd99))
* **zoom-room:** far-end camera discovery + PSTN dial-out ([32d1c06](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/32d1c0692f962c11ea41a6ccd1a734717ab454d3))
* **zoom-room:** log SDK return codes for action commands (remote diagnosis) ([ad599c2](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/ad599c20b3a3a67101537535b3fc4bab9603a3b7))
* **zoom-room:** populate MeetingInfo.CanRecord from SDK recording info ([6c2195c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/6c2195cbfbe1f5d083dab803a58ce8bb126762de))
* **zoom-room:** SIP phone dialing/DTMF + switch to PepperDash package feed ([9908105](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/9908105ac9971bf417da16e838de5cfa52b8e786))
* **zoom-room:** wire content/thumbnail swap, HDMI share, and page-status feedback ([166361d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/166361df72a6bab0d06c1a7f7a179b7dc738a3aa))
* **zoom-room:** wire participant video mute to SDK MuteUserVideo ([812170b](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/812170b121876f1f4dfd10994387886652196454))
* **zoom-room:** wire room output volume (SetVolume/Up/Down/Mute) to SDK ([efb30b7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/efb30b7fefecf11c07c0cbe1d79d86576377d438))
* **zoom-room:** wire self-view, layout paging, and sharing-only meeting to SDK ([5646a63](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/5646a631acae091660abc7b707554fe0b78587d0))
* **zoom:** add EndMeetingForAll device method ([0e256e1](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/0e256e1dd42884f377f2beecbb5d45d7aee2fcd9))
* **zoom:** add forceRepairZoom console command for code rotation ([1aabf0d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/1aabf0d322edf80ad514e0d508b1224990392e99))
* **zoom:** add LogDirectory / InviteContactById console test helpers (R-C/R-D) ([cdf7f8c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/cdf7f8c37f9edecb0a46a3e01f139d2a6801ca01))
* **zoom:** chronological schedule, enriched incoming-call details, waiting-room helpers ([159fe24](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/159fe24d77602e7f0d29ac4a55aae75cba7df424)), closes [#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)
* **zoom:** expose ShowShareInstruction/DismissShareInstruction (F8) ([f5ad95e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f5ad95e3d7c3071d96ef4b086ad3d04308d7c36b))
* **zoom:** guard participant host-controls against invalid/self targets ([1650801](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/1650801637a356610991d429920d68f57105fc42))
* **zoom:** map participant Cohost role (Essentials .7) ([87ec69e](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/87ec69e1537a351ac647ea7975f707657e17cb56)), closes [#4](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/4)
* **zoom:** un-stub camera, participant, and layout features ([f34a51d](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/f34a51d057be964ae3a9b6e215dbf0941f43a475))
* **zoom:** wire camera selection + device list (N2c) ([aab394f](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/aab394fa31ce0dcce6d34b0fed65c4897b955497))
* **zoom:** wire RejectCall to decline meeting invites (N4) ([59715b7](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/59715b79136fe98e50e8a259b170735480e2e3b2))


### Performance Improvements

* **p2:** debounce roster MC posts with 250 ms CTimer ([#31](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/31)) ([7c39c7c](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7c39c7cac9887cbb43057da11950e207431122f3))
* **p2:** defer directory rebuild + publish to final phonebook page ([#30](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/30)) ([07a2824](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/07a28249a2f509114a30dd4d6a7b391959e99115))
* **p2:** skip far-end camera reconciliation when controllable set is unchanged ([#32](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/32)) ([93357a5](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/93357a569af0217a2878d7ce7db6b17f1d980b32))
* **p2:** skip MeetingInfo allocation + event when fields unchanged ([#33](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/issues/33)) ([7b3e677](https://github.com/pepperdash-beincourt/epi-bic-zoomroom/commit/7b3e677481577950ff14923976d661cbd8bceb9d))


### BREAKING CHANGES

* **zoom-room:** the public CameraSelected event signature changed from
EventHandler<CameraSelectedEventArgs> to
EventHandler<CameraSelectedEventArgs<IHasCameraControls>>. Subscribers compiled
against the old non-generic signature must update their handler. (EPI-8;
pre-existing on this branch, documented here.)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
* drops 3-Series / .NET 3.5 CF support. Requires Essentials v3 and a 4-Series processor.
