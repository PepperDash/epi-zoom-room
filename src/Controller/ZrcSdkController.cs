using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.ZoomRoom.Sdk;
using PepperDash.ZoomRoom.Sdk.EventArgs;

namespace PepperDash.Essentials.Plugins
{
    /// <summary>
    /// Production <see cref="IZoomRoomController"/> backed by <see cref="ZrcSdk"/>.
    /// Owns the SDK lifecycle: initializes on construction, disposes on program stop.
    /// </summary>
    public class ZrcSdkController : IZoomRoomController, IKeyed
    {
        private readonly ZrcSdk _sdk;
        private readonly string _sdkConfigPath;
        private readonly string _activationCode;
        private bool _disposed;
        private bool _initialized;
        private string _pendingPassword;
        private CTimer _reconnectTimer;
        private int _reconnectAttempt;
        private const int MaxReconnectAttempts = 10;
        private static readonly int[] ReconnectDelaysMs = { 5000, 10000, 20000, 30000, 60000 };

        public string Key { get; }

        public ZrcSdkController(string key, string sdkConfigPath, string activationCode)
        {
            Key = key;
            _sdkConfigPath  = sdkConfigPath  ?? "/user/zrcsdk";
            _activationCode = activationCode ?? string.Empty;

            // The ZRC SDK loads its native wrapper (libzrcsdkwrapperpdt.so) from /usr/lib by
            // default, but /usr/lib is a read-only filesystem at program runtime. The wrapper is
            // embedded in this assembly; extract it into a writable directory and point the SDK
            // there via ZrcSdk.SetLibraryPath before constructing the SDK.
            StageNativeWrapper();

            // Diagnostic: the wrapper has failed to dlopen on some firmware with "GLIBCXX_x not
            // found". Log the device's actual libstdc++/glibc symbol-version ceilings so we can
            // target a compatible cross-compile toolchain. Runs before SDK creation so it's logged
            // even when the load below throws.
            LogNativeRuntimeCeilings();

            try
            {
                _sdk = new ZrcSdk();
            }
            catch (Exception ex)
            {
                // Constructing the SDK loads/initializes the native wrapper. If that fails the
                // factory's static Serilog logger is invisible (separate plugin load context), so
                // the device just vanishes with a generic "unknown device type". Log via the IKeyed
                // channel (reaches the Essentials log) before letting it propagate.
                this.LogError(ex, "Failed to create ZRC SDK — native wrapper load/init failed; device will not load. {Message}", ex.Message);
                throw;
            }

            // Dispose on program stop — mirrors the ControlSystem example
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatusEvent;
        }

        private const string NativeWrapperFileName = "libzrcsdkwrapperpdt.so";

        /// <summary>
        /// Extracts the native ZRC SDK wrapper embedded in this assembly into a writable directory
        /// next to the plugin and points the SDK at it via <see cref="ZrcSdk.SetLibraryPath"/>.
        /// Essentials' plugin loader deletes the .cplz and its unzip temp dir after moving only the
        /// managed .dll into <c>loadedAssemblies</c>, so the wrapper must travel inside the assembly
        /// as an embedded resource. The SDK loads it via <c>memfd_create</c>/<c>dlopen</c>, so a
        /// <c>noexec</c> writable path is fine. No-op if an up-to-date copy is already present.
        /// </summary>
        private void StageNativeWrapper()
        {
            // The embedded wrapper is linux-arm only. Fail fast on other architectures
            // (e.g. x64 dev/CI machines) with a clear message rather than a cryptic dlopen error (#28).
            if (RuntimeInformation.OSArchitecture != Architecture.Arm &&
                RuntimeInformation.OSArchitecture != Architecture.Arm64)
            {
                this.LogWarning(
                    "Skipping native wrapper staging: architecture {Arch} is not ARM. " +
                    "The embedded '{File}' is linux-arm only and will not load on this host.",
                    RuntimeInformation.OSArchitecture, NativeWrapperFileName);
                return;
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var targetDir = Path.GetDirectoryName(assembly.Location) ?? "/user";
                var targetPath = Path.Combine(targetDir, NativeWrapperFileName);

                using var resourceStream = assembly.GetManifestResourceStream(NativeWrapperFileName);

                if (resourceStream == null)
                {
                    this.LogError(
                        "Embedded native wrapper '{File}' not found in plugin assembly. " +
                        "Available resources: {Resources}. ZRC SDK will fail to load.",
                        NativeWrapperFileName, string.Join(", ", assembly.GetManifestResourceNames()));
                    return;
                }

                if (File.Exists(targetPath) &&
                    new FileInfo(targetPath).Length == resourceStream.Length)
                {
                    this.LogInformation(
                        "Native wrapper already staged at '{Target}' (size matches) — skipping copy.",
                        targetPath);
                    ZrcSdk.SetLibraryPath(targetDir);
                    return;
                }

                using (var fileStream = File.Create(targetPath))
                {
                    resourceStream.CopyTo(fileStream);
                }

                ZrcSdk.SetLibraryPath(targetDir);

                this.LogInformation(
                    "Staged embedded native wrapper '{File}' -> '{Target}' ({Bytes} bytes); SDK library path set to '{Dir}'.",
                    NativeWrapperFileName, targetPath, resourceStream.Length, targetDir);
            }
            catch (Exception ex)
            {
                this.LogError(ex,
                    "Failed to stage native wrapper '{File}': {Message}",
                    NativeWrapperFileName, ex.Message);
            }
        }

        /// <summary>
        /// Reads the device's C++ runtime / glibc shared objects directly (no shell) and logs the
        /// highest <c>GLIBCXX_*</c> and <c>GLIBC_*</c> symbol versions they provide. This is the
        /// compatibility ceiling the cross-compiled wrapper must stay at or below. Best-effort.
        /// </summary>
        private void LogNativeRuntimeCeilings()
        {
            try
            {
                foreach (var p in new[] { "/usr/lib/libstdc++.so.6", "/lib/libstdc++.so.6" })
                    if (File.Exists(p)) { LogMaxSymbolVersion(p, "GLIBCXX_"); break; }

                foreach (var p in new[] { "/lib/libc.so.6", "/usr/lib/libc.so.6", "/lib/arm-linux-gnueabihf/libc.so.6" })
                    if (File.Exists(p)) { LogMaxSymbolVersion(p, "GLIBC_"); break; }
            }
            catch (Exception ex)
            {
                this.LogWarning("Could not read device C++/glibc runtime versions: {Message}", ex.Message);
            }
        }

        private void LogMaxSymbolVersion(string path, string prefix)
        {
            // Stream-scan in fixed-size chunks instead of reading the full (MB-scale) binary into
            // memory. libstdc++ / libc are typically 1-4 MB on ARM; loading them at every boot
            // consumed significant heap for a diagnostic-only operation (#29).
            const int BufSize = 4096;
            var pattern = System.Text.Encoding.Latin1.GetBytes(prefix);
            var buf = new byte[BufSize + 32]; // 32-byte overlap to catch symbols that span chunk boundaries
            Version max = null; string maxStr = null;
            using var fs = File.OpenRead(path);
            int overlap = 0;
            while (true)
            {
                var read = fs.Read(buf, overlap, BufSize);
                if (read == 0) break;
                var len = overlap + read;
                var text = System.Text.Encoding.Latin1.GetString(buf, 0, len);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                    text, System.Text.RegularExpressions.Regex.Escape(prefix) + @"([0-9]+(?:\.[0-9]+)+)"))
                {
                    if (Version.TryParse(m.Groups[1].Value, out var v) && (max == null || v > max))
                    {
                        max = v; maxStr = m.Value;
                    }
                }
                // Preserve last 32 bytes as overlap for the next chunk
                overlap = Math.Min(32, len);
                Buffer.BlockCopy(buf, len - overlap, buf, 0, overlap);
            }
            this.LogInformation("Runtime check: {Path} provides up to {Max}", path, maxStr ?? "(none found)");
        }

        private void OnProgramStatusEvent(eProgramStatusEventType eventType)
        {
            if (eventType == eProgramStatusEventType.Stopping)
            {
                this.LogInformation("Program stopping — disposing ZRC SDK controller.");
                Dispose();
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public bool Initialize(string configPath)
        {
            if (_initialized)
            {
                this.LogWarning("Initialize called more than once — ignoring");
                return false;
            }
            _initialized = true;

            // Wire all SDK events before calling Initialize so no events are missed
            _sdk.Initialized             += (s, e) => SafeRaise(() => Initialized?.Invoke(this, e));
            _sdk.ConnectionStateChanged  += (s, e) =>
            {
                var state = (ConnectionState)e.ErrorCode;
                if (state == ConnectionState.Disconnected)
                {
                    // Disconnect mid-join: ExitMeeting won't fire, so clear any cached password here.
                    _pendingPassword = null;
                    ScheduleReconnect();
                }
                else if (state == ConnectionState.Connected || state == ConnectionState.Established)
                {
                    // Successfully reconnected — cancel any pending retry and reset the counter.
                    CancelReconnect();
                }
                SafeRaise(() => ConnectionStateChanged?.Invoke(this, e));
            };
            _sdk.Error                   += (s, e) => SafeRaise(() => Error?.Invoke(this, e));
            _sdk.PairRoomResult          += (s, e) =>
            {
                this.LogInformation("PairRoomResult: [{ErrorCode}] {Message}",
                    e.ErrorCode, ZrcSdkCodes.GetPairRoomResultDescription(e.ErrorCode));
                SafeRaise(() => PairRoomResult?.Invoke(this, e));
            };
            _sdk.MeetingStatus           += (s, e) => SafeRaise(() => MeetingStatusChanged?.Invoke(this, e));
            _sdk.InstantMeetingStarted   += (s, e) => SafeRaise(() => InstantMeetingStarted?.Invoke(this, e));
            _sdk.StartPmiResult          += (s, e) => SafeRaise(() => StartPmiResult?.Invoke(this, e));
            _sdk.ExitMeeting             += (s, e) =>
            {
                _pendingPassword = null; // C4: don't auto-submit stale password to a later meeting
                SafeRaise(() => ExitMeeting?.Invoke(this, e));
            };
            _sdk.MeetingNeedsPassword    += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_pendingPassword))
                {
                    // Auto-supply the password that was passed with JoinMeetingWithPassword
                    this.LogDebug("MeetingNeedsPassword — auto-submitting cached password");
                    var pw = _pendingPassword;
                    _pendingPassword = null;
                    _sdk.SendMeetingPassword(pw);
                }
                else
                {
                    SafeRaise(() => MeetingNeedsPassword?.Invoke(this, e));
                }
            };
            _sdk.MeetingInvite           += (s, e) => SafeRaise(() => MeetingInvite?.Invoke(this, e));
            _sdk.MeetingLockStatus       += (s, e) => SafeRaise(() => MeetingLockStatusChanged?.Invoke(this, e));
            _sdk.AudioStatus             += (s, e) => SafeRaise(() => AudioMuteStatusChanged?.Invoke(this, e));
            _sdk.RecordingStatus         += (s, e) => SafeRaise(() => RecordingStatusChanged?.Invoke(this, e));
            _sdk.RecordingRequest        += (s, e) => SafeRaise(() => RecordingRequestReceived?.Invoke(this, e));
            _sdk.MeetingRecordingInfoChanged += (s, e) => SafeRaise(() => MeetingRecordingInfoChanged?.Invoke(this, e));
            _sdk.CameraPresetInfoChanged += (s, e) => SafeRaise(() => CameraPresetInfoChanged?.Invoke(this, e));
            _sdk.ParticipantsInitialized += (s, e) => SafeRaise(() => ParticipantsInitialized?.Invoke(this, e));
            _sdk.UserJoined              += (s, e) => SafeRaise(() => UserJoined?.Invoke(this, e));
            _sdk.UserLeft                += (s, e) => SafeRaise(() => UserLeft?.Invoke(this, e));
            _sdk.UserUpdated             += (s, e) => SafeRaise(() => UserUpdated?.Invoke(this, e));
            _sdk.ParticipantCount        += (s, e) => SafeRaise(() => ParticipantCountChanged?.Invoke(this, e));
            _sdk.HostChanged             += (s, e) => SafeRaise(() => HostChanged?.Invoke(this, e));
            _sdk.SharingStatusChanged    += (s, e) => SafeRaise(() => SharingStatusChanged?.Invoke(this, e));
            _sdk.AirPlayStatusChanged    += (s, e) => SafeRaise(() => AirPlayStatusChanged?.Invoke(this, e));
            _sdk.VideoPageStatusChanged  += (s, e) => SafeRaise(() => VideoPageStatusChanged?.Invoke(this, e));
            _sdk.SIPCallStatus           += (s, e) => SafeRaise(() => SipCallStatusChanged?.Invoke(this, e));
            _sdk.ControlSystemEnabled    += (s, e) => SafeRaise(() => ZrcsEnabledChanged?.Invoke(this, e));
            _sdk.ContactListChanged      += (s, e) => SafeRaise(() => ContactListChanged?.Invoke(this, e));
            _sdk.MeetingListChanged      += (s, e) => SafeRaise(() => MeetingListChanged?.Invoke(this, e));

            var effectivePath = string.IsNullOrEmpty(configPath) ? _sdkConfigPath : configPath;
            var result = _sdk.Initialize(effectivePath);
            this.LogInformation("ZrcSdk.Initialize({ConfigPath}) = {Result}", effectivePath, result);

            // Auto-reconnect or pair with activation code
            if (_sdk.CanRetryToPairLastRoom())
            {
                this.LogInformation("Stored pairing credentials found — reconnecting...");
                _sdk.RetryToPairRoom();
            }
            else if (!string.IsNullOrEmpty(_activationCode))
            {
                this.LogInformation("No stored credentials — pairing with activation code.");
                _sdk.PairRoomWithActivationCode(_activationCode);
            }
            else
            {
                this.LogInformation("No stored credentials and no activationCode configured. Use console command 'pairZoomRoom <code>'.");
            }

            return result;
        }

        private void SafeRaise(Action raise)
        {
            try
            {
                raise();
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Exception in SDK event handler: {Message}", ex.Message);
            }
        }

        public int GetConnectionState() => _disposed ? 2 : _sdk.GetConnectionState();

        // ── Pairing ───────────────────────────────────────────────────────────

        public bool PairWithActivationCode(string activationCode) => _sdk.PairRoomWithActivationCode(activationCode);
        public bool CanRetryPair()  => _sdk.CanRetryToPairLastRoom();
        public bool RetryPair()     => _sdk.RetryToPairRoom();
        public bool Unpair()        => _sdk.UnpairRoom();

        public bool RepairWithConfiguredCode()
        {
            if (string.IsNullOrEmpty(_activationCode))
            {
                this.LogWarning("RepairWithConfiguredCode called but no activationCode is configured. Use 'pairZoomRoom <code>' instead.");
                return false;
            }

            this.LogInformation("Clearing stored credentials and re-pairing with configured activation code.");
            _sdk.UnpairRoom();
            return _sdk.PairRoomWithActivationCode(_activationCode);
        }

        // ── Device ────────────────────────────────────────────────────────────

        public bool WakeUp()    => _sdk.WakeZoomRoomUp();
        public bool Logout()    => _sdk.LogoutZoomRoomDevice();
        public bool RestartOs() => _sdk.RestartZoomRoomOS();

        // ── Meeting ───────────────────────────────────────────────────────────

        public bool StartMeeting(string meetingNumber)      => _sdk.StartMeeting(meetingNumber);
        public bool StartInstantMeeting()                   => _sdk.StartInstantMeeting();
        public bool StartMeetingWithHostKey(string hostKey) => _sdk.StartMeetingWithHostKey(hostKey);
        public bool JoinMeeting(string meetingNumber)       => _sdk.JoinMeeting(meetingNumber);
        public bool JoinMeetingWithPassword(string meetingNumber, string password)
        {
            // Cache the password — it will be sent when MeetingNeedsPassword fires
            _pendingPassword = password;
            var result = _sdk.JoinMeeting(meetingNumber);
            if (!result) _pendingPassword = null; // sync rejection — ExitMeeting won't fire
            return result;
        }
        public bool JoinMeetingWithUrl(string url)          => Rc(nameof(JoinMeetingWithUrl), _sdk.JoinMeetingWithURL(url));
        public bool EndMeeting()                            => Rc(nameof(EndMeeting), _sdk.EndMeeting());
        public bool LeaveMeeting()                          => Rc(nameof(LeaveMeeting), _sdk.LeaveMeeting());
        public bool AnswerMeetingInvite(bool accept)        => Rc(nameof(AnswerMeetingInvite), _sdk.AnswerMeetingInvite(accept));
        public bool SendMeetingPassword(string password)    => Rc(nameof(SendMeetingPassword), _sdk.SendMeetingPassword(password));
        public bool CancelEnteringMeetingPassword()         => Rc(nameof(CancelEnteringMeetingPassword), _sdk.CancelEnteringMeetingPassword());
        public bool CancelWaitingForHost()                  => Rc(nameof(CancelWaitingForHost), _sdk.CancelWaitingForHost());
        public bool LockMeeting(bool locked)                => Rc(nameof(LockMeeting), _sdk.LockMeeting(locked));

        // ── SDK call result logging ─────────────────────────────────────────────
        // The SDK returns a ZRCSDKError (0 = success) or a bool; ZoomRoom otherwise discards it.
        // Log non-zero / failed results at Warning so SDK rejections (wrong meeting state, feature
        // not available, etc.) are visible during remote testing — a console "method called" only
        // means the C# call didn't throw, not that the SDK acted. Successes log at Debug.
        private int Rc(string op, int code)
        {
            if (code != 0) this.LogWarning("SDK call {Op} returned error code {Code}", op, code);
            else this.LogDebug("SDK call {Op} ok", op);
            return code;
        }
        private bool Rc(string op, bool ok)
        {
            if (!ok) this.LogWarning("SDK call {Op} returned failure", op);
            else this.LogDebug("SDK call {Op} ok", op);
            return ok;
        }

        // ── Audio ─────────────────────────────────────────────────────────────

        public bool SetAudioMute(bool mute)                    => Rc(nameof(SetAudioMute), _sdk.SetAudioMute(mute));
        public bool MuteUserAudio(int userId, bool mute)       => Rc(nameof(MuteUserAudio), _sdk.MuteUserAudio(userId, mute));
        public bool MuteAllAudio(bool mute)                    => Rc(nameof(MuteAllAudio), _sdk.MuteAllAudio(mute));
        public bool SetMuteOnEntry(bool mute)                  => Rc(nameof(SetMuteOnEntry), _sdk.SetMuteOnEntry(mute));
        public bool AnswerUnmuteRequest(bool accepted)         => Rc(nameof(AnswerUnmuteRequest), _sdk.AnswerUnmuteRequest(accepted));
        public bool AllowAttendeesUnmute(bool allow)           => Rc(nameof(AllowAttendeesUnmute), _sdk.AllowAttendeesUnmute(allow));
        public bool SetSpeakerVolume(float volume)             => Rc(nameof(SetSpeakerVolume), _sdk.SetSpeakerVolume(volume));
        public float GetSpeakerVolume()                        => _sdk.GetSpeakerVolume(out var v) ? v : -1f;

        // ── Video ─────────────────────────────────────────────────────────────

        public bool SetVideoState(bool start)                  => Rc(nameof(SetVideoState), _sdk.SetVideoState(start));
        public bool MuteUserVideo(int userId, bool mute)       => Rc(nameof(MuteUserVideo), _sdk.MuteUserVideo(userId, mute));
        public bool PinUserOnScreen(int userId, int screenIndex = 0)    => Rc(nameof(PinUserOnScreen), _sdk.PinUserOnScreen(userId, screenIndex));
        public bool UnpinUserFromScreen(int userId, int screenIndex = 0) => Rc(nameof(UnpinUserFromScreen), _sdk.UnpinUserFromScreen(userId, screenIndex));
        public bool ControlUserCamera(int userId, int action, int type) => Rc(nameof(ControlUserCamera), _sdk.ControlUserCamera(userId, action, type));
        public bool ControlCamera(string deviceId, int action, int type)    => Rc(nameof(ControlCamera), _sdk.ControlCamera(deviceId, action, type));
        public bool ChangeSmartCameraMode(int mask, string deviceId = "")   => Rc(nameof(ChangeSmartCameraMode), _sdk.ChangeSmartCameraMode(mask, deviceId));
        public CameraDevice[] GetCameras()              => _sdk.GetCameras();
        public CameraDevice GetCurrentCamera()          => _sdk.TryGetCurrentCamera(out var cam) ? cam : null;
        public bool SetCurrentCamera(string deviceId)   => Rc(nameof(SetCurrentCamera), _sdk.SetCurrentCamera(deviceId));
        public bool SetCameraPreset(uint index, string deviceId)  => Rc(nameof(SetCameraPreset), _sdk.SetCameraPreset(index, deviceId ?? ""));
        public bool GoToCameraPreset(uint index, string deviceId) => Rc(nameof(GoToCameraPreset), _sdk.GoToCameraPreset(index, deviceId ?? ""));
        public bool NameCameraPreset(uint index, string name, string deviceId) => Rc(nameof(NameCameraPreset), _sdk.NameCameraPreset(index, name ?? "", deviceId ?? ""));

        // ── Layout ────────────────────────────────────────────────────────────

        public int SetScreenLayout(int screen, int layoutSourceType) => Rc(nameof(SetScreenLayout), _sdk.SetScreenLayout(screen, layoutSourceType));
        public int SetVideoOrder(int videoOrderType)                 => Rc(nameof(SetVideoOrder), _sdk.SetVideoOrder(videoOrderType));
        public int UpdateVideoLayoutStyle(int videoLayoutStyle)      => Rc(nameof(UpdateVideoLayoutStyle), _sdk.UpdateVideoLayoutStyle(videoLayoutStyle));
        public int ControlVideoPosition(int position, int size)      => Rc(nameof(ControlVideoPosition), _sdk.ControlVideoPosition(position, size));
        public int TurnVideoPage(bool forward, int pageVideoType)    => Rc(nameof(TurnVideoPage), _sdk.TurnVideoPage(forward, pageVideoType));
        public int ChangeThumbnailsPosition(int type)                => Rc(nameof(ChangeThumbnailsPosition), _sdk.ChangeThumbnailsPosition(type));
        public int SwitchToFloatingShareForSingleScreen(bool floatingShare) => Rc(nameof(SwitchToFloatingShareForSingleScreen), _sdk.SwitchToFloatingShareForSingleScreen(floatingShare));

        // ── Recording ─────────────────────────────────────────────────────────

        public bool StartRecording()                               => Rc(nameof(StartRecording), _sdk.StartRecording());
        public bool StopRecording()                                => Rc(nameof(StopRecording), _sdk.StopRecording());
        public bool PauseRecording()                               => Rc(nameof(PauseRecording), _sdk.PauseRecording());
        public bool ResumeRecording()                              => Rc(nameof(ResumeRecording), _sdk.ResumeRecording());
        public bool ResponseToRecordingRequest(bool accept, bool acceptAlways = false)
            => Rc(nameof(ResponseToRecordingRequest), _sdk.ResponseToRecordingRequest(accept, acceptAlways));

        // ── Participants ──────────────────────────────────────────────────────

        public int GetParticipantCount() => _sdk.GetParticipantCount();
        public bool ExpelUser(int userId)  => Rc(nameof(ExpelUser), _sdk.ExpelUser(userId));
        public bool AssignHost(int userId) => Rc(nameof(AssignHost), _sdk.AssignHost(userId));

        // ── Share ─────────────────────────────────────────────────────────────

        public bool StopShare() => Rc(nameof(StopShare), _sdk.StopShare());
        public bool LaunchSharingMeeting(bool isInLocalShare, int displayState) => Rc(nameof(LaunchSharingMeeting), _sdk.LaunchSharingMeeting(isInLocalShare, displayState));
        public bool SwitchFromLocalPresentationToNormalMeeting()                => Rc(nameof(SwitchFromLocalPresentationToNormalMeeting), _sdk.SwitchFromLocalPresentationToNormalMeeting());
        public bool ShowSharingInstruction(bool show, int instructionState)     => Rc(nameof(ShowSharingInstruction), _sdk.ShowSharingInstruction(show, instructionState));
        public bool ShareBlackMagic(bool isStart, bool isViewLocally)           => Rc(nameof(ShareBlackMagic), _sdk.ShareBlackMagic(isStart, isViewLocally));

        // ── Waiting room ──────────────────────────────────────────────────────

        public bool AdmitUserFromWaitingRoom(int userId)  => Rc(nameof(AdmitUserFromWaitingRoom), _sdk.AdmitUserFromWaitingRoom(userId));
        public bool AdmitAllFromWaitingRoom()             => Rc(nameof(AdmitAllFromWaitingRoom), _sdk.AdmitAllFromWaitingRoom());
        public bool PutUserInWaitingRoom(int userId)      => Rc(nameof(PutUserInWaitingRoom), _sdk.PutUserInWaitingRoom(userId));

        // ── SIP / Phone ───────────────────────────────────────────────────────

        public bool TerminateSipCall(string callId)               => Rc(nameof(TerminateSipCall), _sdk.TerminateSIPCall(callId));
        public bool CallSip(string uri)                           => Rc(nameof(CallSip), _sdk.CallSIP(uri));
        public bool SendDtmfToSipCall(string dtmf, string callId) => Rc(nameof(SendDtmfToSipCall), _sdk.SendDTMFToSIPCall(dtmf, callId));
        public bool CallOutPstnUser(string phoneNumber, bool cancelCall, bool hasVoicePrompt) => Rc(nameof(CallOutPstnUser), _sdk.CallOutPSTNUser(phoneNumber, cancelCall, hasVoicePrompt));

        // ── Contacts / Directory ──────────────────────────────────────────────

        public bool SubscribeContacts(int startIndex, int count, bool searchSip) => Rc(nameof(SubscribeContacts), _sdk.SubscribeContacts(startIndex, count, searchSip));
        public bool InviteAttendees(string[] contactIds)                          => Rc(nameof(InviteAttendees), _sdk.InviteAttendees(contactIds));
        public bool MeetWithIMUsers(string[] contactIds)                          => Rc(nameof(MeetWithIMUsers), _sdk.MeetWithIMUsers(contactIds));

        // ── Bookings / Schedule ────────────────────────────────────────────────

        public bool ListMeeting() => Rc(nameof(ListMeeting), _sdk.ListMeeting());

        // ── ZRCS ──────────────────────────────────────────────────────────────

        public bool IsZrcsEnabled() => _sdk.IsZRCSEnabled();

        // ── Events ───────────────────────────────────────────────────────────

        public event EventHandler<SdkEventArgs> Initialized;
        public event EventHandler<SdkEventArgs> ConnectionStateChanged;
        public event EventHandler<SdkEventArgs> Error;
        public event EventHandler<SdkEventArgs> PairRoomResult;
        public event EventHandler<SdkEventArgs> MeetingStatusChanged;
        public event EventHandler<SdkEventArgs> InstantMeetingStarted;
        public event EventHandler<SdkEventArgs> StartPmiResult;
        public event EventHandler<SdkEventArgs> ExitMeeting;
        public event EventHandler<SdkEventArgs> MeetingNeedsPassword;
        public event EventHandler<MeetingInviteEventArgs> MeetingInvite;
        public event EventHandler<SdkEventArgs> MeetingLockStatusChanged;
        public event EventHandler<SdkEventArgs> AudioMuteStatusChanged;
        public event EventHandler<SdkEventArgs> RecordingStatusChanged;
        public event EventHandler<SdkEventArgs> RecordingRequestReceived;
        public event EventHandler<MeetingRecordingInfoEventArgs> MeetingRecordingInfoChanged;
        public event EventHandler<CameraPresetInfoEventArgs> CameraPresetInfoChanged;
        public event EventHandler<ParticipantListEventArgs> ParticipantsInitialized;
        public event EventHandler<ParticipantListEventArgs> UserJoined;
        public event EventHandler<ParticipantListEventArgs> UserLeft;
        public event EventHandler<ParticipantListEventArgs> UserUpdated;
        public event EventHandler<SdkEventArgs> ParticipantCountChanged;
        public event EventHandler<SdkEventArgs> HostChanged;
        public event EventHandler<SharingStatusEventArgs> SharingStatusChanged;
        public event EventHandler<AirPlayStatusEventArgs> AirPlayStatusChanged;
        public event EventHandler<VideoPageStatusEventArgs> VideoPageStatusChanged;
        public event EventHandler<SIPCall> SipCallStatusChanged;
        public event EventHandler<SdkEventArgs> ZrcsEnabledChanged;
        public event EventHandler<ContactListEventArgs> ContactListChanged;
        public event EventHandler<MeetingListEventArgs> MeetingListChanged;

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelReconnect();
            CrestronEnvironment.ProgramStatusEventHandler -= OnProgramStatusEvent;
            try
            {
                _sdk.Dispose();
                this.LogInformation("ZRC SDK disposed.");
            }
            catch (Exception ex)
            {
                this.LogError("Error disposing ZRC SDK: {Message}", ex.Message);
            }
        }

        private void ScheduleReconnect()
        {
            if (_disposed) return;
            if (!_sdk.CanRetryToPairLastRoom())
            {
                this.LogWarning("Disconnected and no stored pairing credentials — cannot auto-reconnect.");
                return;
            }

            _reconnectAttempt++;
            if (_reconnectAttempt > MaxReconnectAttempts)
            {
                this.LogWarning("Auto-reconnect exceeded {Max} attempts — giving up. Use 'repairZoomRoom' to retry.", MaxReconnectAttempts);
                return;
            }

            var delayMs = ReconnectDelaysMs[Math.Min(_reconnectAttempt - 1, ReconnectDelaysMs.Length - 1)];
            this.LogInformation("Disconnected — scheduling reconnect attempt {Attempt}/{Max} in {Delay}ms",
                _reconnectAttempt, MaxReconnectAttempts, delayMs);

            _reconnectTimer?.Dispose();
            _reconnectTimer = new CTimer(_ =>
            {
                if (_disposed) return;
                this.LogInformation("Auto-reconnect attempt {Attempt}: calling RetryToPairRoom()", _reconnectAttempt);
                _sdk.RetryToPairRoom();
            }, null, delayMs);
        }

        private void CancelReconnect()
        {
            _reconnectAttempt = 0;
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }
    }
}
