using System;
using System.IO;
using System.Reflection;
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

            _sdk = new ZrcSdk();

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
            _sdk.ConnectionStateChanged  += (s, e) => SafeRaise(() => ConnectionStateChanged?.Invoke(this, e));
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
            _sdk.ParticipantsInitialized += (s, e) => SafeRaise(() => ParticipantsInitialized?.Invoke(this, e));
            _sdk.UserJoined              += (s, e) => SafeRaise(() => UserJoined?.Invoke(this, e));
            _sdk.UserLeft                += (s, e) => SafeRaise(() => UserLeft?.Invoke(this, e));
            _sdk.UserUpdated             += (s, e) => SafeRaise(() => UserUpdated?.Invoke(this, e));
            _sdk.ParticipantCount        += (s, e) => SafeRaise(() => ParticipantCountChanged?.Invoke(this, e));
            _sdk.HostChanged             += (s, e) => SafeRaise(() => HostChanged?.Invoke(this, e));
            _sdk.SharingStatusChanged    += (s, e) => SafeRaise(() => SharingStatusChanged?.Invoke(this, e));
            _sdk.SIPCallStatus           += (s, e) => SafeRaise(() => SipCallStatusChanged?.Invoke(this, e));
            _sdk.ControlSystemEnabled    += (s, e) => SafeRaise(() => ZrcsEnabledChanged?.Invoke(this, e));

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
                this.LogError("Exception in SDK event handler: {Message}", ex.Message);
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
            return _sdk.JoinMeeting(meetingNumber);
        }
        public bool JoinMeetingWithUrl(string url)          => _sdk.JoinMeetingWithURL(url);
        public bool EndMeeting()                            => _sdk.EndMeeting();
        public bool LeaveMeeting()                          => _sdk.LeaveMeeting();
        public bool SendMeetingPassword(string password)    => _sdk.SendMeetingPassword(password);
        public bool CancelEnteringMeetingPassword()         => _sdk.CancelEnteringMeetingPassword();
        public bool CancelWaitingForHost()                  => _sdk.CancelWaitingForHost();
        public bool LockMeeting(bool locked)                => _sdk.LockMeeting(locked);

        // ── Audio ─────────────────────────────────────────────────────────────

        public bool SetAudioMute(bool mute)                    => _sdk.SetAudioMute(mute);
        public bool MuteUserAudio(int userId, bool mute)       => _sdk.MuteUserAudio(userId, mute);
        public bool MuteAllAudio(bool mute)                    => _sdk.MuteAllAudio(mute);
        public bool SetMuteOnEntry(bool mute)                  => _sdk.SetMuteOnEntry(mute);
        public bool AnswerUnmuteRequest(bool accepted)         => _sdk.AnswerUnmuteRequest(accepted);
        public bool AllowAttendeesUnmute(bool allow)           => _sdk.AllowAttendeesUnmute(allow);

        // ── Video ─────────────────────────────────────────────────────────────

        public bool SetVideoState(bool start)                  => _sdk.SetVideoState(start);
        public bool MuteUserVideo(int userId, bool mute)       => _sdk.MuteUserVideo(userId, mute);
        public bool PinUserOnScreen(int userId, int screenIndex = 0)    => _sdk.PinUserOnScreen(userId, screenIndex);
        public bool UnpinUserFromScreen(int userId, int screenIndex = 0) => _sdk.UnpinUserFromScreen(userId, screenIndex);
        public bool ControlUserCamera(int userId, int action, int type) => _sdk.ControlUserCamera(userId, action, type);

        // ── Layout ────────────────────────────────────────────────────────────

        public int SetScreenLayout(int screen, int layoutSourceType) => _sdk.SetScreenLayout(screen, layoutSourceType);
        public int SetVideoOrder(int videoOrderType)                 => _sdk.SetVideoOrder(videoOrderType);
        public int UpdateVideoLayoutStyle(int videoLayoutStyle)      => _sdk.UpdateVideoLayoutStyle(videoLayoutStyle);
        public int ControlVideoPosition(int position, int size)      => _sdk.ControlVideoPosition(position, size);
        public int TurnVideoPage(bool forward, int pageVideoType)    => _sdk.TurnVideoPage(forward, pageVideoType);
        public int ChangeThumbnailsPosition(int type)                => _sdk.ChangeThumbnailsPosition(type);

        // ── Recording ─────────────────────────────────────────────────────────

        public bool StartRecording()                               => _sdk.StartRecording();
        public bool StopRecording()                                => _sdk.StopRecording();
        public bool PauseRecording()                               => _sdk.PauseRecording();
        public bool ResumeRecording()                              => _sdk.ResumeRecording();
        public bool ResponseToRecordingRequest(bool accept, bool acceptAlways = false)
            => _sdk.ResponseToRecordingRequest(accept, acceptAlways);

        // ── Participants ──────────────────────────────────────────────────────

        public int GetParticipantCount() => _sdk.GetParticipantCount();

        // ── Share ─────────────────────────────────────────────────────────────

        public bool StopShare() => _sdk.StopShare();
        public bool LaunchSharingMeeting(bool isInLocalShare, int displayState) => _sdk.LaunchSharingMeeting(isInLocalShare, displayState);
        public bool SwitchFromLocalPresentationToNormalMeeting()                => _sdk.SwitchFromLocalPresentationToNormalMeeting();
        public bool ShowSharingInstruction(bool show, int instructionState)     => _sdk.ShowSharingInstruction(show, instructionState);

        // ── Waiting room ──────────────────────────────────────────────────────

        public bool AdmitUserFromWaitingRoom(int userId)  => _sdk.AdmitUserFromWaitingRoom(userId);
        public bool AdmitAllFromWaitingRoom()             => _sdk.AdmitAllFromWaitingRoom();
        public bool PutUserInWaitingRoom(int userId)      => _sdk.PutUserInWaitingRoom(userId);

        // ── SIP / Phone ───────────────────────────────────────────────────────

        public bool TerminateSipCall(string callId) => _sdk.TerminateSIPCall(callId);

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
        public event EventHandler<SdkEventArgs> MeetingInvite;
        public event EventHandler<SdkEventArgs> MeetingLockStatusChanged;
        public event EventHandler<SdkEventArgs> AudioMuteStatusChanged;
        public event EventHandler<SdkEventArgs> RecordingStatusChanged;
        public event EventHandler<SdkEventArgs> RecordingRequestReceived;
        public event EventHandler<ParticipantListEventArgs> ParticipantsInitialized;
        public event EventHandler<ParticipantListEventArgs> UserJoined;
        public event EventHandler<ParticipantListEventArgs> UserLeft;
        public event EventHandler<ParticipantListEventArgs> UserUpdated;
        public event EventHandler<SdkEventArgs> ParticipantCountChanged;
        public event EventHandler<SdkEventArgs> HostChanged;
        public event EventHandler<SharingStatusEventArgs> SharingStatusChanged;
        public event EventHandler<SIPCall> SipCallStatusChanged;
        public event EventHandler<SdkEventArgs> ZrcsEnabledChanged;

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
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
    }
}
