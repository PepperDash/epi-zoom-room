using System;
using PepperDash.ZoomRoom.Sdk;
using PepperDash.ZoomRoom.Sdk.EventArgs;

namespace PepperDash.Essentials.Plugins
{
    /// <summary>
    /// Abstraction over the ZRC SDK surface used by <see cref="ZoomRoom"/>.
    /// Implemented by <see cref="ZrcSdkController"/> for production use; can be
    /// replaced with a test double in unit tests.
    /// </summary>
    public interface IZoomRoomController : IDisposable
    {
        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>Initialize the SDK with the given config directory path.</summary>
        bool Initialize(string configPath);

        /// <summary>Returns the current connection state (0=Established, 1=Connected, 2=Disconnected).</summary>
        int GetConnectionState();

        // ── Pairing ───────────────────────────────────────────────────────────

        bool PairWithActivationCode(string activationCode);
        bool CanRetryPair();
        bool RetryPair();
        bool Unpair();

        /// <summary>
        /// Clears any stored pairing credentials and re-pairs using the activation code
        /// from configuration. Use this after rotating the activation code, since a stale
        /// stored token otherwise forces the reconnect path and ignores the new code.
        /// </summary>
        bool RepairWithConfiguredCode();

        // ── Device ────────────────────────────────────────────────────────────

        bool WakeUp();
        bool Logout();
        bool RestartOs();

        // ── Meeting ───────────────────────────────────────────────────────────

        bool StartMeeting(string meetingNumber);
        bool StartInstantMeeting();
        bool StartMeetingWithHostKey(string hostKey);
        bool JoinMeeting(string meetingNumber);
        bool JoinMeetingWithPassword(string meetingNumber, string password);
        bool JoinMeetingWithUrl(string url);
        bool EndMeeting();
        bool LeaveMeeting();
        bool SendMeetingPassword(string password);
        bool CancelEnteringMeetingPassword();
        bool CancelWaitingForHost();
        bool LockMeeting(bool locked);

        // ── Audio ─────────────────────────────────────────────────────────────

        bool SetAudioMute(bool mute);
        bool MuteUserAudio(int userId, bool mute);
        bool MuteAllAudio(bool mute);
        bool SetMuteOnEntry(bool mute);
        bool AnswerUnmuteRequest(bool accepted);
        bool AllowAttendeesUnmute(bool allow);

        // ── Video ─────────────────────────────────────────────────────────────

        bool SetVideoState(bool start);
        bool MuteUserVideo(int userId, bool mute);
        bool PinUserOnScreen(int userId, int screenIndex = 0);
        bool UnpinUserFromScreen(int userId, int screenIndex = 0);

        /// <summary>Controls a far-end (participant) camera. action = CameraControlAction, type = CameraControlType (Start/Continue/Stop).</summary>
        bool ControlUserCamera(int userId, int action, int type);

        // ── Layout ────────────────────────────────────────────────────────────

        int SetScreenLayout(int screen, int layoutSourceType);
        int SetVideoOrder(int videoOrderType);

        /// <summary>Sets the meeting video layout style (VideoLayoutStyle: Gallery=1, Speaker=2, Thumbnail=3, ContentOnly=4, DynamicLayout=6). Distinct from SetVideoOrder, which only reorders tiles.</summary>
        int UpdateVideoLayoutStyle(int videoLayoutStyle);

        /// <summary>Sets the self-view PiP position and size. position = VideoThumbPosition, size = VideoThumbSize (Off=0 hides the PiP).</summary>
        int ControlVideoPosition(int position, int size);

        /// <summary>Pages the gallery/thumbnail/dynamic video view. pageVideoType = PageVideoType (GalleryView=0, ThumbnailView=1, DynamicLayoutView=2).</summary>
        int TurnVideoPage(bool forward, int pageVideoType);

        /// <summary>Changes the thumbnail strip position (content/thumbnail swap). type = ThumbnailsPositionType.</summary>
        int ChangeThumbnailsPosition(int type);

        // ── Recording ─────────────────────────────────────────────────────────

        bool StartRecording();
        bool StopRecording();
        bool PauseRecording();
        bool ResumeRecording();
        bool ResponseToRecordingRequest(bool accept, bool acceptAlways = false);

        // ── Participants ──────────────────────────────────────────────────────

        int GetParticipantCount();

        // ── Share ─────────────────────────────────────────────────────────────

        bool StopShare();

        /// <summary>Launches a sharing-only ("local presentation") meeting. displayState = SharingInstructionDisplayState.</summary>
        bool LaunchSharingMeeting(bool isInLocalShare, int displayState);

        /// <summary>Switches an active local presentation into a normal Zoom meeting.</summary>
        bool SwitchFromLocalPresentationToNormalMeeting();

        /// <summary>Shows/hides the wireless-share instruction overlay. instructionState = SharingInstructionDisplayState.</summary>
        bool ShowSharingInstruction(bool show, int instructionState);

        // ── Waiting room ──────────────────────────────────────────────────────

        bool AdmitUserFromWaitingRoom(int userId);
        bool AdmitAllFromWaitingRoom();
        bool PutUserInWaitingRoom(int userId);

        // ── SIP / Phone ───────────────────────────────────────────────────────

        bool TerminateSipCall(string callId);

        // ── ZRCS ──────────────────────────────────────────────────────────────

        bool IsZrcsEnabled();

        // ── Events ───────────────────────────────────────────────────────────

        event EventHandler<SdkEventArgs> Initialized;
        event EventHandler<SdkEventArgs> ConnectionStateChanged;
        event EventHandler<SdkEventArgs> Error;
        event EventHandler<SdkEventArgs> PairRoomResult;
        event EventHandler<SdkEventArgs> MeetingStatusChanged;
        event EventHandler<SdkEventArgs> InstantMeetingStarted;
        event EventHandler<SdkEventArgs> StartPmiResult;
        event EventHandler<SdkEventArgs> ExitMeeting;
        event EventHandler<SdkEventArgs> MeetingNeedsPassword;
        event EventHandler<SdkEventArgs> MeetingInvite;
        event EventHandler<SdkEventArgs> MeetingLockStatusChanged;
        event EventHandler<SdkEventArgs> AudioMuteStatusChanged;
        event EventHandler<SdkEventArgs> RecordingStatusChanged;
        event EventHandler<SdkEventArgs> RecordingRequestReceived;
        event EventHandler<ParticipantListEventArgs> ParticipantsInitialized;
        event EventHandler<ParticipantListEventArgs> UserJoined;
        event EventHandler<ParticipantListEventArgs> UserLeft;
        event EventHandler<ParticipantListEventArgs> UserUpdated;
        event EventHandler<SdkEventArgs> ParticipantCountChanged;
        event EventHandler<SdkEventArgs> HostChanged;
        event EventHandler<SharingStatusEventArgs> SharingStatusChanged;
        event EventHandler<SIPCall> SipCallStatusChanged;
        event EventHandler<SdkEventArgs> ZrcsEnabledChanged;
    }
}
