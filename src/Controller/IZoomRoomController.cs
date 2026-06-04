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

        /// <summary>Sets the room speaker (audio output) volume, in the SDK's native float scale.</summary>
        bool SetSpeakerVolume(float volume);

        /// <summary>Gets the room speaker (audio output) volume in the SDK's native float scale; returns -1 on failure.</summary>
        float GetSpeakerVolume();

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

        /// <summary>Changes the thumbnail strip position (top/bottom). type = ThumbnailsPositionType.</summary>
        int ChangeThumbnailsPosition(int type);

        /// <summary>Swaps shared content with the participant video on a single screen. floatingShare = float the share (video full-screen).</summary>
        int SwitchToFloatingShareForSingleScreen(bool floatingShare);

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

        /// <summary>Starts/stops an HDMI ("black magic") cable share. isViewLocally = also show the source locally.</summary>
        bool ShareBlackMagic(bool isStart, bool isViewLocally);

        // ── Waiting room ──────────────────────────────────────────────────────

        bool AdmitUserFromWaitingRoom(int userId);
        bool AdmitAllFromWaitingRoom();
        bool PutUserInWaitingRoom(int userId);

        // ── SIP / Phone ───────────────────────────────────────────────────────

        bool TerminateSipCall(string callId);

        /// <summary>Places an outbound SIP call to the given URI / number.</summary>
        bool CallSip(string uri);

        /// <summary>Sends DTMF digits to an active SIP call (empty callId targets the single active call).</summary>
        bool SendDtmfToSipCall(string dtmf, string callId);

        /// <summary>
        /// Dials out a PSTN phone number into the current meeting. <paramref name="cancelCall"/> cancels an
        /// in-progress call-out; <paramref name="hasVoicePrompt"/> rings the call on the Zoom Room.
        /// </summary>
        bool CallOutPstnUser(string phoneNumber, bool cancelCall, bool hasVoicePrompt);

        // ── Contacts / Directory ──────────────────────────────────────────────

        /// <summary>
        /// Subscribes to a range of directory contacts. Results arrive via <see cref="ContactListChanged"/>.
        /// <paramref name="searchSip"/> requests SIP contacts instead of IM contacts.
        /// </summary>
        bool SubscribeContacts(int startIndex, int count, bool searchSip);

        /// <summary>Invites the given contacts (by contact ID) into the CURRENT meeting.</summary>
        bool InviteAttendees(string[] contactIds);

        /// <summary>Starts a NEW meeting with the given contacts (by contact ID).</summary>
        bool MeetWithImUsers(string[] contactIds);

        // ── Bookings / Schedule ────────────────────────────────────────────────

        /// <summary>
        /// Requests the current list of scheduled meetings (calendar bookings).
        /// Results arrive via <see cref="MeetingListChanged"/>.
        /// </summary>
        bool ListMeeting();

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
        event EventHandler<MeetingRecordingInfoEventArgs> MeetingRecordingInfoChanged;
        event EventHandler<ParticipantListEventArgs> ParticipantsInitialized;
        event EventHandler<ParticipantListEventArgs> UserJoined;
        event EventHandler<ParticipantListEventArgs> UserLeft;
        event EventHandler<ParticipantListEventArgs> UserUpdated;
        event EventHandler<SdkEventArgs> ParticipantCountChanged;
        event EventHandler<SdkEventArgs> HostChanged;
        event EventHandler<SharingStatusEventArgs> SharingStatusChanged;
        event EventHandler<VideoPageStatusEventArgs> VideoPageStatusChanged;
        event EventHandler<SIPCall> SipCallStatusChanged;
        event EventHandler<SdkEventArgs> ZrcsEnabledChanged;
        event EventHandler<ContactListEventArgs> ContactListChanged;
        event EventHandler<MeetingListEventArgs> MeetingListChanged;
    }
}
