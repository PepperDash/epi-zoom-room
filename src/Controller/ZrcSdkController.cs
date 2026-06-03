using System;
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

        public string Key { get; }

        public ZrcSdkController(string key, string sdkConfigPath, string activationCode)
        {
            Key = key;
            _sdkConfigPath  = sdkConfigPath  ?? "/user/zrcsdk";
            _activationCode = activationCode ?? string.Empty;

            _sdk = new ZrcSdk();

            // Dispose on program stop — mirrors the ControlSystem example
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatusEvent;
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
            // Wire all SDK events before calling Initialize so no events are missed
            _sdk.Initialized             += (s, e) => Initialized?.Invoke(this, e);
            _sdk.ConnectionStateChanged  += (s, e) => ConnectionStateChanged?.Invoke(this, e);
            _sdk.Error                   += (s, e) => Error?.Invoke(this, e);
            _sdk.PairRoomResult          += (s, e) =>
            {
                this.LogInformation("PairRoomResult: [{ErrorCode}] {Message}",
                    e.ErrorCode, ZrcSdkCodes.GetPairRoomResultDescription(e.ErrorCode));
                PairRoomResult?.Invoke(this, e);
            };
            _sdk.MeetingStatus           += (s, e) => MeetingStatusChanged?.Invoke(this, e);
            _sdk.InstantMeetingStarted   += (s, e) => InstantMeetingStarted?.Invoke(this, e);
            _sdk.StartPmiResult          += (s, e) => StartPmiResult?.Invoke(this, e);
            _sdk.ExitMeeting             += (s, e) => ExitMeeting?.Invoke(this, e);
            _sdk.MeetingNeedsPassword    += (s, e) => MeetingNeedsPassword?.Invoke(this, e);
            _sdk.MeetingInvite           += (s, e) => MeetingInvite?.Invoke(this, e);
            _sdk.MeetingLockStatus       += (s, e) => MeetingLockStatusChanged?.Invoke(this, e);
            _sdk.AudioStatus             += (s, e) => AudioMuteStatusChanged?.Invoke(this, e);
            _sdk.RecordingStatus         += (s, e) => RecordingStatusChanged?.Invoke(this, e);
            _sdk.RecordingRequest        += (s, e) => RecordingRequestReceived?.Invoke(this, e);
            _sdk.ParticipantsInitialized += (s, e) => ParticipantsInitialized?.Invoke(this, e);
            _sdk.UserJoined              += (s, e) => UserJoined?.Invoke(this, e);
            _sdk.UserLeft                += (s, e) => UserLeft?.Invoke(this, e);
            _sdk.UserUpdated             += (s, e) => UserUpdated?.Invoke(this, e);
            _sdk.ParticipantCount        += (s, e) => ParticipantCountChanged?.Invoke(this, e);
            _sdk.HostChanged             += (s, e) => HostChanged?.Invoke(this, e);
            _sdk.SharingStatusChanged    += (s, e) => SharingStatusChanged?.Invoke(this, e);
            _sdk.SIPCallStatus           += (s, e) => SipCallStatusChanged?.Invoke(this, e);
            _sdk.ControlSystemEnabled    += (s, e) => ZrcsEnabledChanged?.Invoke(this, e);

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

        public int GetConnectionState() => _disposed ? 2 : _sdk.GetConnectionState();

        // ── Pairing ───────────────────────────────────────────────────────────

        public bool PairWithActivationCode(string activationCode) => _sdk.PairRoomWithActivationCode(activationCode);
        public bool CanRetryPair()  => _sdk.CanRetryToPairLastRoom();
        public bool RetryPair()     => _sdk.RetryToPairRoom();
        public bool Unpair()        => _sdk.UnpairRoom();

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
            var result = _sdk.JoinMeeting(meetingNumber);
            if (result && !string.IsNullOrEmpty(password))
                _sdk.SendMeetingPassword(password);
            return result;
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

        // ── Layout ────────────────────────────────────────────────────────────

        public int SetScreenLayout(int screen, int layoutSourceType) => _sdk.SetScreenLayout(screen, layoutSourceType);
        public int SetVideoOrder(int videoOrderType)                 => _sdk.SetVideoOrder(videoOrderType);

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
