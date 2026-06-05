using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using PepperDash.ZoomRoom.Sdk;
using PepperDash.ZoomRoom.Sdk.EventArgs;

namespace PepperDash.Essentials.Plugins
{
	public class ZoomRoom : VideoCodecBase, IHasCodecSelfView, IHasDirectoryHistoryStack, ICommunicationMonitor,
		IHasScheduleAwareness, IHasCodecCameras, IHasParticipants, IHasCameraOff, IHasCameraMuteWithUnmuteReqeust, IHasCameraAutoMode,
		IHasFarEndContentStatus, IHasSelfviewPosition, IHasPhoneDialing, IHasZoomRoomLayouts, IHasParticipantPinUnpin,
		IHasParticipantAudioMute, IHasSelfviewSize, IPasswordPrompt, IHasStartMeeting, IHasMeetingInfo, IHasPresentationOnlyMeeting,
        IHasMeetingLock, IHasMeetingRecordingWithPrompt, IZoomWirelessShareInstructions
	{
#pragma warning disable CS0067 // Required by IHasCameraMuteWithUnmuteReqeust; never raised because Zoom Room SDK handles video state directly
        public event EventHandler VideoUnmuteRequested;
#pragma warning restore CS0067

		private const long MeetingRefreshTimer = 60000;
        public uint DefaultMeetingDurationMin { get; private set; }

        // ── SDK-backed state (replaces the old zStatus/zConfiguration model) ──
        private readonly IZoomRoomController _controller;
        private bool _sdkAudioMuted;
        private bool _sdkCameraOff;
        private bool _cameraAutoModeOn; // tracks SmartCameraMask: SpeakerFocus(auto) vs Manual
        private bool _sdkIsRecording;
        private bool _sdkCanRecord; // room "can start recording" — from MeetingRecordingInfo.canIRecord
        private bool _sdkMeetingLocked;
        private bool _sdkIsHost;
        // SDK gap: ZrcSdk does not surface a host-name event; _sdkHostName stays empty.
        // If the SDK adds a HostChanged payload in a future version, populate it there.
        private string _sdkHostName = string.Empty;
        private int  _sdkSharingState; // 0 = not sharing
        private string _currentMeetingId     = string.Empty;
        private string _currentMeetingNumber = string.Empty;
        private string _currentMeetingName   = string.Empty;
        private string _activeSipCallId      = string.Empty;
        private bool _meetingPasswordRequired;
        // Layout page state, driven by the SDK's VideoPageStatus notification.
        private bool _layoutIsOnFirstPage;
        private bool _layoutIsOnLastPage;
        private int  _currentPageVideoType; // PageVideoType (0 = GalleryView)
        private bool _contentSwappedWithThumbnail;
        // Room speaker (audio output) volume state. Level is the Essentials 0-65535 range.
        private ushort _sdkSpeakerVolumeLevel;
        private bool _sdkSpeakerMuted;

        // True once the SDK reports the room Connected/Established. Gates outbound command methods so
        // join/start/invite actions issued before the room is paired/connected are dropped with a clear
        // log entry instead of silently failing inside the SDK (the old ZoomRoomSyncState used to gate this).
        private volatile bool _isConnected;

        private readonly object _participantLock = new object();
        // Raw SDK participant info keyed by userID, kept in sync with Participants.CurrentParticipants.
        // Carries the far-end camera-control flags the Essentials Participant type does not, so
        // UpdateFarEndCameras() can discover which participants expose controllable cameras.
        private readonly Dictionary<int, ParticipantInfo> _participantInfoByUserId = new Dictionary<int, ParticipantInfo>();
        // Directory/phonebook contacts accumulated from the SDK contact subscription (R-D), keyed by
        // contact ID. The subscription delivers contacts in pages; merging here lets DirectoryRoot be
        // rebuilt from the union of all batches received.
        private readonly object _directoryLock = new object();
        private readonly Dictionary<string, ContactInfo> _directoryContactsById = new Dictionary<string, ContactInfo>();
        private IHasCameraControls _selectedCamera;
        private CodecDirectory _currentDirectoryResult;

        private readonly ZoomRoomPropertiesConfig _props;

        public ZoomRoom(DeviceConfig config, IZoomRoomController controller, ZoomRoomPropertiesConfig props)
			: base(config)
		{
            DefaultMeetingDurationMin = 30;

			_props = props;

            _controller = controller;

			Status        = new ZoomRoomStatus();
			Configuration = new ZoomRoomConfiguration();

			CommunicationMonitor = new SdkConnectionMonitor(this);
			DeviceManager.AddDevice(CommunicationMonitor);

			CodecInfo = new ZoomRoomInfo(Status, Configuration);

			PhonebookSyncState = new CodecPhonebookSyncState(Key + "--PhonebookSync");

			CodecOsdIn = new RoutingInputPort(RoutingPortNames.CodecOsd,
				eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Hdmi, new Action(StopSharing), this);

			Output1 = new RoutingOutputPort(RoutingPortNames.HdmiOut1,
				eRoutingSignalType.Audio | eRoutingSignalType.Video,
				eRoutingPortConnectionType.Hdmi, null, this);

			Output2 = new RoutingOutputPort(RoutingPortNames.HdmiOut2,
				eRoutingSignalType.Video,
				eRoutingPortConnectionType.DisplayPort, null, this);

            Output3 = new RoutingOutputPort(RoutingPortNames.HdmiOut3,
                eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, null, this);

			SelfviewIsOnFeedback = new BoolFeedback(SelfViewIsOnFeedbackFunc);

			CameraIsOffFeedback = new BoolFeedback(CameraIsOffFeedbackFunc);

			CameraIsMutedFeedback = CameraIsOffFeedback;

			CameraAutoModeIsOnFeedback = new BoolFeedback(CameraAutoModeIsOnFeedbackFunc);

			CodecSchedule = new CodecScheduleAwareness(MeetingRefreshTimer);

		    if (_props.MinutesBeforeMeetingStart > 0)
		    {
		        CodecSchedule.MeetingWarningMinutes = _props.MinutesBeforeMeetingStart;
		    }

			ReceivingContent = new BoolFeedback(FarEndIsSharingContentFeedbackFunc);

			SelfviewPipPositionFeedback = new StringFeedback(SelfviewPipPositionFeedbackFunc);

			// TODO: #714 [ ] SelfviewPipSizeFeedback
			SelfviewPipSizeFeedback = new StringFeedback(SelfviewPipSizeFeedbackFunc);

			SetUpFeedbackActions();

			Cameras = new List<IHasCameraControls>();

			// Initialized here (not in SetUpCameras, which only runs once connected) so the
			// SelectedCamera setter never dereferences a null feedback.
			SelectedCameraFeedback = new StringFeedback(() => _selectedCamera != null ? _selectedCamera.Key : string.Empty);
			ControllingFarEndCameraFeedback = new BoolFeedback(() => SelectedCamera is IAmFarEndCamera);

			SetUpDirectory();

			Participants = new CodecParticipants();

			SupportsCameraOff = true; // Always allow turning off the camera for zoom calls?
			SupportsCameraAutoMode = _props.SupportsCameraAutoMode;

			PhoneOffHookFeedback = new BoolFeedback(PhoneOffHookFeedbackFunc);
			CallerIdNameFeedback = new StringFeedback(CallerIdNameFeedbackFunc);
			CallerIdNumberFeedback = new StringFeedback(CallerIdNumberFeedbackFunc);

			LocalLayoutFeedback = new StringFeedback(LocalLayoutFeedbackFunc);

			LayoutViewIsOnFirstPageFeedback = new BoolFeedback(LayoutViewIsOnFirstPageFeedbackFunc);
			LayoutViewIsOnLastPageFeedback = new BoolFeedback(LayoutViewIsOnLastPageFeedbackFunc);
			CanSwapContentWithThumbnailFeedback = new BoolFeedback(CanSwapContentWithThumbnailFeedbackFunc);
			ContentSwappedWithThumbnailFeedback = new BoolFeedback(ContentSwappedWithThumbnailFeedbackFunc);

			NumberOfScreensFeedback = new IntFeedback(NumberOfScreensFeedbackFunc);

            MeetingIsLockedFeedback = new BoolFeedback(() => _sdkMeetingLocked);

            MeetingIsRecordingFeedback = new BoolFeedback(() => _sdkIsRecording);

            RecordConsentPromptIsVisible = new BoolFeedback(() => _recordConsentPromptIsVisible);

            SetUpRouting();
		}

		public CommunicationGather PortGather { get; private set; }

		public ZoomRoomStatus Status { get; private set; }

		public ZoomRoomConfiguration Configuration { get; private set; }

		//CTimer LoginMessageReceivedTimer;
		//CTimer RetryConnectionTimer;

		/// <summary>
		/// Gets and returns the scaled volume of the codec
		/// </summary>
		protected override Func<int> VolumeLevelFeedbackFunc
		{
			get { return () => _sdkSpeakerVolumeLevel; } // room audio-output (speaker) level, 0-65535
		}

		protected override Func<bool> PrivacyModeIsOnFeedbackFunc
		{
			get { return () => _sdkAudioMuted; }
		}

		protected override Func<bool> StandbyIsOnFeedbackFunc
		{
			get { return () => false; }
		}

		protected override Func<string> SharingSourceFeedbackFunc
		{
			get { return () => _sdkSharingState != 0 ? "Sharing" : "None"; }
		}

		protected override Func<bool> SharingContentIsOnFeedbackFunc
		{
			get { return () => _sdkSharingState != 0; }
		}

		// KNOWN LIMITATION: the ZRC SDK's sharing status (SharingStatusEventArgs.SharingState, surfaced via
		// OnControllerSharingStatusChanged) reports only THIS room's sharing — there is no far-end/remote
		// sharing signal. ReceivingContent is therefore hardwired false and must NOT be relied upon to detect
		// a remote participant sharing. Revisit if/when the SDK exposes a far-end sharing flag.
		protected Func<bool> FarEndIsSharingContentFeedbackFunc
		{
			get { return () => false; }
		}

		protected override Func<bool> MuteFeedbackFunc
		{
			get { return () => _sdkSpeakerMuted; } // room audio-output mute (volume zeroed)
		}

		//protected Func<bool> RoomIsOccupiedFeedbackFunc
		//{
		//    get
		//    {
		//        return () => false;
		//    }
		//}

		//protected Func<int> PeopleCountFeedbackFunc
		//{
		//    get
		//    {
		//        return () => 0;
		//    }
		//}

		protected Func<bool> SelfViewIsOnFeedbackFunc
		{
			// Self-view is "on" whenever the PiP size is not Off.
			get { return () => _currentSelfviewPipSize != null
				&& !"Off".Equals(_currentSelfviewPipSize.Command, StringComparison.OrdinalIgnoreCase); }
		}

		protected Func<bool> CameraIsOffFeedbackFunc
		{
			get { return () => _sdkCameraOff; }
		}

		protected Func<bool> CameraAutoModeIsOnFeedbackFunc
		{
			get { return () => _cameraAutoModeOn; }
		}

		protected Func<string> SelfviewPipPositionFeedbackFunc
		{
			get
			{
				return
					() =>
						_currentSelfviewPipPosition != null
							? _currentSelfviewPipPosition.Command ?? "Unknown"
							: "Unknown";
			}
		}

		// TODO: #714 [ ] SelfviewPipSizeFeedbackFunc
		protected Func<string> SelfviewPipSizeFeedbackFunc
		{
			get
			{
				return
					() =>
						_currentSelfviewPipSize != null
							? _currentSelfviewPipSize.Command ?? "Unknown"
							: "Unknown";
			}
		}

		protected Func<bool> LocalLayoutIsProminentFeedbackFunc
		{
			get { return () => false; }
		}


		public RoutingInputPort CodecOsdIn { get; private set; }
		public RoutingOutputPort Output1 { get; private set; }
		public RoutingOutputPort Output2 { get; private set; }
        public RoutingOutputPort Output3 { get; private set; }

		#region ICommunicationMonitor Members

		public StatusMonitorBase CommunicationMonitor { get; private set; }

		#endregion

		#region IHasCodecCameras Members

		// BREAKING CHANGE (v3 migration): this event's signature changed from the non-generic
		// EventHandler<CameraSelectedEventArgs> to EventHandler<CameraSelectedEventArgs<IHasCameraControls>>.
		// Subscribers compiled against the old signature must update their handler to the generic args type.
		public event EventHandler<CameraSelectedEventArgs<IHasCameraControls>> CameraSelected;

		public List<IHasCameraControls> Cameras { get; private set; }

		public IHasCameraControls SelectedCamera
		{
			get { return _selectedCamera; }
			private set
			{
				_selectedCamera = value;
				SelectedCameraFeedback.FireUpdate();
				ControllingFarEndCameraFeedback.FireUpdate();

				var handler = CameraSelected;
				if (handler != null)
				{
					handler(this, new CameraSelectedEventArgs<IHasCameraControls>(_selectedCamera));
				}
			}
		}


		public StringFeedback SelectedCameraFeedback { get; private set; }

		public void SelectCamera(string key)
        {
            // Read the camera list under _participantLock — SetUpCameras and UpdateFarEndCameras
            // mutate Cameras from the SDK connection/participant threads.
            IHasCameraControls camera;
            lock (_participantLock)
                camera = Cameras.FirstOrDefault(c => c.Key.Equals(key));

            if (camera == null)
            {
                this.LogWarning("SelectCamera: no camera with key {Key}", key);
                return;
            }

            // Far-end cameras are selected locally only (no SDK device switch); near-end cameras
            // switch the room's active camera via the setting service, keyed by device ID.
            if (camera is IAmFarEndCamera)
            {
                SelectedCamera = camera;
                return;
            }

            if (_controller.SetCurrentCamera(key))
            {
                SelectedCamera = camera;
            }
            else
            {
                // SDK rejected the switch (wrong meeting state, device unavailable). Leave SelectedCamera
                // unchanged and re-assert the true current selection so the UI doesn't desync from intent.
                this.LogWarning("SelectCamera: SDK rejected switch to {Key}; selection unchanged.", key);
                SelectedCameraFeedback.FireUpdate();
            }
        }

		public CameraBase FarEndCamera { get; private set; }

		public BoolFeedback ControllingFarEndCameraFeedback { get; private set; }

		#endregion

		#region IHasCodecSelfView Members

		public BoolFeedback SelfviewIsOnFeedback { get; private set; }

		/// <summary>
		/// Re-emits the self-view feedback from plugin-cached state. NOTE: the ZRC SDK exposes no
		/// self-view query, so this does NOT refresh from the device — a self-view change made on the
		/// native Zoom Room UI is not reflected until this plugin next sets it. (IHasCodecSelfView
		/// requires this method name; it cannot be renamed to signal the cached-only behavior.)
		/// </summary>
		public void GetSelfViewMode() { SelfviewIsOnFeedback.FireUpdate(); }

		public void SelfViewModeOn()
		{
			// Restore the last visible PiP size (default Size1); a non-Off size shows the self-view.
			var size = _lastVisibleSelfviewPipSize
				?? SelfviewPipSizes.FirstOrDefault(s => s.Command.Equals("Size1", StringComparison.OrdinalIgnoreCase))
				?? SelfviewPipSizes.FirstOrDefault(s => !s.Command.Equals("Off", StringComparison.OrdinalIgnoreCase));
			if (size != null) SelfviewPipSizeSet(size);
		}

		public void SelfViewModeOff()
		{
			var off = SelfviewPipSizes.FirstOrDefault(s => s.Command.Equals("Off", StringComparison.OrdinalIgnoreCase));
			if (off != null) SelfviewPipSizeSet(off);
		}

		public void SelfViewModeToggle()
		{
			if (SelfviewIsOnFeedback.BoolValue)
			{
				SelfViewModeOff();
			}
			else
			{
				SelfViewModeOn();
			}
		}

		#endregion

		#region IHasDirectoryHistoryStack Members

		public event EventHandler<DirectoryEventArgs> DirectoryResultReturned;
		public CodecDirectory DirectoryRoot { get; private set; }

		public CodecDirectory CurrentDirectoryResult
		{
			get { return _currentDirectoryResult; }
			private set
			{
				_currentDirectoryResult = value;

				this.LogDebug("CurrentDirectoryResult Updated.  ResultsFolderId: {ResultsFolderId}  Contact Count: {ContactCount}",
					_currentDirectoryResult.ResultsFolderId, _currentDirectoryResult.CurrentDirectoryResults.Count);

				OnDirectoryResultReturned(_currentDirectoryResult);
			}
		}

		public CodecPhonebookSyncState PhonebookSyncState { get; private set; }

		public void SearchDirectory(string searchString)
		{
			var directoryResults = new CodecDirectory();

			directoryResults.AddContactsToDirectory(
				DirectoryRoot.CurrentDirectoryResults.FindAll(
					c => c.Name.IndexOf(searchString, 0, StringComparison.OrdinalIgnoreCase) > -1));

			DirectoryBrowseHistoryStack.Clear();
			CurrentDirectoryResult = directoryResults;
		}

		public void GetDirectoryFolderContents(string folderId)
		{
			var directoryResults = new CodecDirectory {ResultsFolderId = folderId};

			directoryResults.AddContactsToDirectory(
				DirectoryRoot.CurrentDirectoryResults.FindAll(c => c.ParentFolderId.Equals(folderId)));

			DirectoryBrowseHistoryStack.Push(_currentDirectoryResult);

			CurrentDirectoryResult = directoryResults;
		}

		public void SetCurrentDirectoryToRoot()
		{
			DirectoryBrowseHistoryStack.Clear();

			CurrentDirectoryResult = DirectoryRoot;
		}

		public void GetDirectoryParentFolderContents()
		{
			if (DirectoryBrowseHistoryStack.Count == 0)
			{
				return;
			}

			var currentDirectory = DirectoryBrowseHistoryStack.Pop();

			CurrentDirectoryResult = currentDirectory;
		}

		public BoolFeedback CurrentDirectoryResultIsNotDirectoryRoot { get; private set; }

		public List<CodecDirectory> DirectoryBrowseHistory { get; private set; }

		public Stack<CodecDirectory> DirectoryBrowseHistoryStack { get; private set; }

		#endregion

		#region IHasScheduleAwareness Members

		public CodecScheduleAwareness CodecSchedule { get; private set; }

		public void GetSchedule()
		{
			_controller.ListMeeting();
		}

		#endregion

		#region IRouting Members

		public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
		{
			ExecuteSwitch(inputSelector);
		}

		#endregion



        private void SetUpCallFeedbackActions() { /* No-op: feedback driven by SDK events */ }

        private void HandleCallRecordInfoStateUpdate(object sender, PropertyChangedEventArgs a)
        {
            MeetingIsRecordingFeedback.FireUpdate();
        }

        private void HandleCallStateUpdate(object sender, PropertyChangedEventArgs a)
        {
            // No-op: call state driven by SDK events
        }

	    private void HandleSharingStateUpdate(object sender, PropertyChangedEventArgs a)
	    {
            SharingContentIsOnFeedback.FireUpdate();
            ReceivingContent.FireUpdate();
	    }

		/// <summary>
		/// Subscribes to the PropertyChanged events on the state objects and fires the corresponding feedbacks.
		/// </summary>
		private void SetUpFeedbackActions()
		{
            // All feedbacks are now driven by SDK events (see OnController* handlers).
            // The legacy Configuration.*/Status.* PropertyChanged subscriptions have been removed
            // because those models are no longer fed now that the JSON-over-SSH pipeline is gone.
            SetUpCallFeedbackActions();
		}

		private void SetUpDirectory()
		{
			DirectoryRoot = new CodecDirectory() {ResultsFolderId = "root"};

			CurrentDirectoryResultIsNotDirectoryRoot = new BoolFeedback(() => CurrentDirectoryResult.ResultsFolderId != "root");

			CurrentDirectoryResult = DirectoryRoot;

			DirectoryBrowseHistory = new List<CodecDirectory>();
			DirectoryBrowseHistoryStack = new Stack<CodecDirectory>();
		}

		private void SetUpRouting()
		{
			// Set up input ports
			CreateOsdSource();
			InputPorts.Add(CodecOsdIn);

			// Set up output ports
			OutputPorts.Add(Output1);
			OutputPorts.Add(Output2);
		}

		/// <summary>
		/// Creates the fake OSD source, and connects it's AudioVideo output to the CodecOsdIn input
		/// to enable routing 
		/// </summary>
		private void CreateOsdSource()
		{
			OsdSource = new DummyRoutingInputsDevice(Key + "[osd]");
			DeviceManager.AddDevice(OsdSource);
			var tl = new TieLine(OsdSource.AudioVideoOutputPort, CodecOsdIn);
			TieLineCollection.Default.Add(tl);

			//foreach(var input in Status.Video.
		}

		/// <summary>
		/// Starts the HTTP feedback server and syncronizes state of codec
		/// </summary>
		/// <returns></returns>
		protected override bool CustomActivate()
		{
			CrestronConsole.AddNewConsoleCommand(
				s => { if (!string.IsNullOrWhiteSpace(s)) _controller.PairWithActivationCode(s.Trim()); },
				"pairZoomRoom", "Pair Zoom Room with activation code", ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(
				s => _controller.RetryPair(),
				"repairZoomRoom", "Reconnect to last paired Zoom Room", ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(
				s => _controller.Unpair(),
				"unpairZoomRoom", "Unpair from Zoom Room", ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(
				s => _controller.RepairWithConfiguredCode(),
				"forceRepairZoom", "Clear stored credentials and re-pair using the configured activation code", ConsoleAccessLevelEnum.AccessOperator);

			return base.CustomActivate();
		}

	    #region Overrides of Device

	    protected override void Initialize()
	    {
            _controller.ConnectionStateChanged   += OnControllerConnectionStateChanged;
            _controller.PairRoomResult           += (s, e) => this.LogInformation("PairRoomResult [{Code}]: {Desc}", e.ErrorCode, ZrcSdkCodes.GetPairRoomResultDescription(e.ErrorCode));
            _controller.MeetingStatusChanged     += OnControllerMeetingStatusChanged;
            _controller.InstantMeetingStarted    += OnControllerInstantMeetingStarted;
            _controller.StartPmiResult           += OnControllerStartPmiResult;
            _controller.ExitMeeting              += OnControllerExitMeeting;
            _controller.MeetingNeedsPassword     += OnControllerMeetingNeedsPassword;
            _controller.MeetingInvite            += OnControllerMeetingInvite;
            _controller.MeetingLockStatusChanged += OnControllerMeetingLockStatusChanged;
            _controller.AudioMuteStatusChanged   += OnControllerAudioMuteStatusChanged;
            _controller.RecordingStatusChanged   += OnControllerRecordingStatusChanged;
            _controller.MeetingRecordingInfoChanged += OnControllerMeetingRecordingInfoChanged;
            _controller.RecordingRequestReceived += OnControllerRecordingRequestReceived;
            _controller.ParticipantsInitialized  += OnControllerParticipantsInitialized;
            _controller.UserJoined               += OnControllerUserJoined;
            _controller.UserLeft                 += OnControllerUserLeft;
            _controller.UserUpdated              += OnControllerUserUpdated;
            _controller.ParticipantCountChanged  += (s, e) => Participants.OnParticipantsChanged();
            _controller.HostChanged              += OnControllerHostChanged;
            _controller.SharingStatusChanged     += OnControllerSharingStatusChanged;
            _controller.VideoPageStatusChanged   += OnControllerVideoPageStatusChanged;
            _controller.SipCallStatusChanged     += OnControllerSipCallStatusChanged;
            _controller.ContactListChanged       += OnControllerContactListChanged;
            _controller.MeetingListChanged       += OnControllerMeetingListChanged;

            if (!_controller.Initialize(_props.SdkConfigPath))
            {
                // Surface init failures (bad/missing SdkConfigPath, native wrapper load failure, etc.)
                // instead of silently proceeding as if the SDK were ready. _isConnected stays false,
                // so command methods are gated off (see EnsureConnected) and the comms monitor stays offline.
                this.LogError("ZRC SDK controller failed to initialize (sdkConfigPath=\"{Path}\"). Device will not be functional.", _props.SdkConfigPath);
            }
	    }

	    #endregion

        // ── SDK event handlers ───────────────────────────────────────────────

        private void OnControllerConnectionStateChanged(object sender, SdkEventArgs e)
        {
            var state = (ConnectionState)e.ErrorCode;
            var online = state == ConnectionState.Established || state == ConnectionState.Connected;
            this.LogInformation("SDK connection state changed: {State} ({Code})", state, e.ErrorCode);

            _isConnected = online;
            ((SdkConnectionMonitor)CommunicationMonitor).SetOnline(online);

            // Fetch initial data only once fully Connected. At Established the SDK service helpers
            // (contacts, meeting list, settings) aren't ready yet, so these calls return failure;
            // the Connected event that follows pairing is when they succeed.
            if (state == ConnectionState.Connected)
            {
                SeedSpeakerVolume();

                // Populate near-end cameras from the SDK device list (real device IDs).
                SetUpCameras();

                // Auto-download the directory/phonebook unless disabled by config. Results arrive
                // asynchronously via ContactListChanged and populate DirectoryRoot.
                if (!_props.DisablePhonebookAutoDownload)
                {
                    this.LogInformation("Requesting directory contacts (auto-download)");
                    _controller.SubscribeContacts(0, 50, false);
                }

                // Request the current schedule/bookings. Results arrive asynchronously via
                // MeetingListChanged and populate CodecSchedule.
                this.LogInformation("Requesting meeting schedule (bookings)");
                _controller.ListMeeting();
            }
            else if (!online)
            {
                // Reset in-call state on disconnect
                ActiveCalls.Clear();
                Participants.CurrentParticipants = new System.Collections.Generic.List<Participant>();
                OnCallStatusChange(new CodecActiveCallItem { Status = eCodecCallStatus.Disconnected });

                lock (_directoryLock) _directoryContactsById.Clear();
                PhonebookSyncState.CodecDisconnected();
            }
        }

        // Readiness gate for outbound commands. Returns false (and logs) when the SDK is not yet
        // Connected/Established, so callers can no-op instead of firing a command the SDK will reject.
        private bool EnsureConnected(string operation)
        {
            if (_isConnected) return true;
            this.LogWarning("{Operation} ignored: ZRC SDK is not connected yet.", operation);
            return false;
        }

        // Reads the current room speaker volume once on connect so the volume feedback reflects
        // the real level. There is no SDK push for output-volume changes, so external (Zoom UI)
        // changes won't update the feedback until the next set from this plugin.
        private void SeedSpeakerVolume()
        {
            var sdkVolume = _controller.GetSpeakerVolume();
            if (sdkVolume < 0f) return; // get failed (e.g. setting service not ready yet)
            _sdkSpeakerVolumeLevel = (ushort)Math.Round(Math.Max(0f, Math.Min(SdkSpeakerVolumeMax, sdkVolume)) / SdkSpeakerVolumeMax * 65535f);
            _sdkSpeakerMuted = _sdkSpeakerVolumeLevel == 0;
            VolumeLevelFeedback.FireUpdate();
            MuteFeedback.FireUpdate();
        }

        private void OnControllerMeetingStatusChanged(object sender, SdkEventArgs e)
        {
            var status = (MeetingStatus)e.ErrorCode;
            this.LogInformation("MeetingStatusChanged: {Status} ({Code})", status, e.ErrorCode);

            switch (status)
            {
                case MeetingStatus.InMeeting:
                {
                    if (ActiveCalls.Count == 0)
                    {
                        var call = new CodecActiveCallItem
                        {
                            Name    = _currentMeetingName,
                            Number  = _currentMeetingNumber,
                            Id      = _currentMeetingId,
                            Status  = eCodecCallStatus.Connected,
                            Type    = eCodecCallType.Video,
                        };
                        ActiveCalls.Add(call);
                        OnCallStatusChange(call);
                    }
                    else
                    {
                        var existing = ActiveCalls.FirstOrDefault();
                        if (existing != null)
                        {
                            existing.Status = eCodecCallStatus.Connected;
                            OnCallStatusChange(existing);
                        }
                    }
                    UpdateMeetingInfo();
                    break;
                }
                case MeetingStatus.ConnectingToMeeting:
                {
                    if (ActiveCalls.Count == 0)
                    {
                        var call = new CodecActiveCallItem
                        {
                            Name    = _currentMeetingName,
                            Number  = _currentMeetingNumber,
                            Id      = _currentMeetingId,
                            Status  = eCodecCallStatus.Connecting,
                            Type    = eCodecCallType.Video,
                        };
                        ActiveCalls.Add(call);
                        OnCallStatusChange(call);
                    }
                    break;
                }
                case MeetingStatus.NotInMeeting:
                case MeetingStatus.LoggedOut:
                {
                    _currentMeetingId     = string.Empty;
                    _currentMeetingNumber = string.Empty;
                    _currentMeetingName   = string.Empty;
                    _sdkIsHost            = false;
                    _sdkHostName          = string.Empty;
                    if (ActiveCalls.Count > 0)
                    {
                        var call = ActiveCalls.FirstOrDefault();
                        if (call != null)
                        {
                            call.Status = eCodecCallStatus.Disconnected;
                            OnCallStatusChange(call);
                            ActiveCalls.Remove(call);
                        }
                    }
                    UpdateMeetingInfo();
                    break;
                }
            }
        }

        private void OnControllerInstantMeetingStarted(object sender, SdkEventArgs e)
        {
            this.LogInformation("InstantMeetingStarted code={Code} meetingNumber={Number}", e.ErrorCode, e.Message);
            if (!string.IsNullOrEmpty(e.Message))
            {
                _currentMeetingNumber = e.Message;
                _currentMeetingId     = e.Message;
            }
            UpdateMeetingInfo();
        }

        private void OnControllerStartPmiResult(object sender, SdkEventArgs e)
        {
            this.LogInformation("StartPmiResult code={Code} meetingNumber={Number}", e.ErrorCode, e.Message);
            if (!string.IsNullOrEmpty(e.Message))
            {
                _currentMeetingNumber = e.Message;
                _currentMeetingId     = e.Message;
            }
            UpdateMeetingInfo();
        }

        private void OnControllerExitMeeting(object sender, SdkEventArgs e)
        {
            this.LogInformation("ExitMeeting reason={Reason} ({Code})", (ExitMeetingReason)e.ErrorCode, e.ErrorCode);
            _currentMeetingId     = string.Empty;
            _currentMeetingNumber = string.Empty;
            _currentMeetingName   = string.Empty;
            _sdkIsHost            = false;
            _sdkHostName          = string.Empty;
            _sdkMeetingLocked     = false;
            _sdkIsRecording       = false;
            ActiveCalls.Clear();
            Participants.CurrentParticipants = new System.Collections.Generic.List<Participant>();
            OnCallStatusChange(new CodecActiveCallItem { Status = eCodecCallStatus.Disconnected });
            UpdateMeetingInfo();
        }

        private void OnControllerMeetingNeedsPassword(object sender, SdkEventArgs e)
        {
            var wrongAndRetry = e.ErrorCode == 1;
            OnPasswordRequired(wrongAndRetry, false, false, "Password required to join this meeting.");
        }

        private void OnControllerMeetingInvite(object sender, SdkEventArgs e)
        {
            // SDK MeetingInvite fires when a meeting invitation arrives (e.g., a call-in meeting invite)
            this.LogInformation("MeetingInvite received code={Code}", e.ErrorCode);
        }

        private void OnControllerMeetingLockStatusChanged(object sender, SdkEventArgs e)
        {
            _sdkMeetingLocked = e.ErrorCode == 1;
            MeetingIsLockedFeedback.FireUpdate();
            UpdateMeetingInfo();
        }

        private void OnControllerAudioMuteStatusChanged(object sender, SdkEventArgs e)
        {
            _sdkAudioMuted = e.ErrorCode == 1;
            PrivacyModeIsOnFeedback.FireUpdate();
        }

        private void OnControllerRecordingStatusChanged(object sender, SdkEventArgs e)
        {
            _sdkIsRecording = e.ErrorCode == 1;
            MeetingIsRecordingFeedback.FireUpdate();
            UpdateMeetingInfo();
        }

        private void OnControllerMeetingRecordingInfoChanged(object sender, MeetingRecordingInfoEventArgs e)
        {
            _sdkCanRecord = e.CanIRecord;
            UpdateMeetingInfo(); // refreshes MeetingInfo.CanRecord on the bridge join
        }

        private void OnControllerRecordingRequestReceived(object sender, SdkEventArgs e)
        {
            _recordConsentPromptIsVisible = true;
            RecordConsentPromptIsVisible.FireUpdate();
        }

        private void OnControllerParticipantsInitialized(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            lock (_participantLock)
            {
                Participants.CurrentParticipants = MapParticipants(e.Participants);
                TrackParticipantInfo(e.Participants, fullReplace: true, isLeave: false);
            }
            UpdateFarEndCameras();
        }

        private void OnControllerUserJoined(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            lock (_participantLock)
            {
                if (e.NeedCleanUp)
                {
                    Participants.CurrentParticipants = MapParticipants(e.Participants);
                }
                else
                {
                    foreach (var info in e.Participants)
                    {
                        var existing = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId == info.UserID);
                        if (existing == null)
                        {
                            var p = MapParticipant(info);
                            Participants.CurrentParticipants.Add(p);
                        }
                    }
                }
                TrackParticipantInfo(e.Participants, fullReplace: e.NeedCleanUp, isLeave: false);
            }
            Participants.OnParticipantsChanged();
            UpdateFarEndCameras();
        }

        private void OnControllerUserLeft(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            lock (_participantLock)
            {
                if (e.NeedCleanUp)
                {
                    Participants.CurrentParticipants = MapParticipants(e.Participants);
                }
                else
                {
                    foreach (var info in e.Participants)
                    {
                        var existing = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId == info.UserID);
                        if (existing != null)
                            Participants.CurrentParticipants.Remove(existing);
                    }
                }
                // On a clean-up event e.Participants is the remaining full roster; otherwise it lists who left.
                TrackParticipantInfo(e.Participants, fullReplace: e.NeedCleanUp, isLeave: !e.NeedCleanUp);
            }
            Participants.OnParticipantsChanged();
            UpdateFarEndCameras();
        }

        private void OnControllerUserUpdated(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            lock (_participantLock)
            {
                if (e.NeedCleanUp)
                {
                    Participants.CurrentParticipants = MapParticipants(e.Participants);
                }
                else
                {
                    foreach (var info in e.Participants)
                    {
                        var existing = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId == info.UserID);
                        if (existing != null)
                        {
                            existing.Name           = info.UserName;
                            existing.IsHost         = info.IsHost;
                            existing.AudioMuteFb    = info.AudioMuted;
                            existing.VideoMuteFb    = !info.VideoSending;
                            existing.HandIsRaisedFb = info.HandRaised;
                        }
                    }
                }
                TrackParticipantInfo(e.Participants, fullReplace: e.NeedCleanUp, isLeave: false);
            }
            Participants.OnParticipantsChanged();
            UpdateFarEndCameras();
        }

        private void OnControllerHostChanged(object sender, SdkEventArgs e)
        {
            _sdkIsHost = e.ErrorCode == 1;
            this.LogDebug("HostChanged: isHost={IsHost}", _sdkIsHost);
            UpdateMeetingInfo();
        }

        private void OnControllerSharingStatusChanged(object sender, SharingStatusEventArgs e)
        {
            _sdkSharingState = e.SharingState;
            SharingContentIsOnFeedback.FireUpdate();
            ReceivingContent.FireUpdate();
        }

        private void OnControllerVideoPageStatusChanged(object sender, VideoPageStatusEventArgs e)
        {
            _layoutIsOnFirstPage  = e.IsInFirstPage;
            _layoutIsOnLastPage   = e.IsInLastPage;
            _currentPageVideoType = e.PageVideoType; // keep the SDK's current page type for TurnVideoPage
            LayoutViewIsOnFirstPageFeedback.FireUpdate();
            LayoutViewIsOnLastPageFeedback.FireUpdate();
        }

        private void OnControllerSipCallStatusChanged(object sender, SIPCall e)
        {
            _activeSipCallId = e?.CallID ?? string.Empty;
            this.LogDebug("SipCallStatusChanged: CallID={CallId}", _activeSipCallId);
        }

        private static System.Collections.Generic.List<Participant> MapParticipants(ParticipantInfo[] participants)
        {
            var list = new System.Collections.Generic.List<Participant>();
            foreach (var info in participants)
                list.Add(MapParticipant(info));
            return list;
        }

        private static Participant MapParticipant(ParticipantInfo info)
        {
            return new Participant
            {
                UserId         = info.UserID,
                Name           = info.UserName,
                IsHost         = info.IsHost,
                IsMyself       = info.IsMySelf,
                AudioMuteFb    = info.AudioMuted,
                VideoMuteFb    = !info.VideoSending,
                HandIsRaisedFb = info.HandRaised,
                // IsPinnedFb: SDK does not expose per-participant pin state; defaults to false.
            };
        }


        protected override void OnCallStatusChange(CodecActiveCallItem item)
        {
            base.OnCallStatusChange(item);
        }

        /// <summary>
        /// Starts sharing HDMI source
        /// </summary>
		/// <summary>
		/// Starts sharing the HDMI source (Zoom "black magic" cable share), also shown locally.
		/// </summary>
		public override void StartSharing() { _controller.ShareBlackMagic(true, true); }

		/// <summary>
		/// Stops sharing the current presentation
		/// </summary>
		public override void StopSharing() { _controller.StopShare(); }

		

		public override void PrivacyModeOn()
		{
			_controller.SetAudioMute(true);
		}

		public override void PrivacyModeOff()
		{
			_controller.SetAudioMute(false);
		}

		public override void PrivacyModeToggle()
		{
			if (PrivacyModeIsOnFeedback.BoolValue)
			{
				PrivacyModeOff();
			}
			else
			{
				PrivacyModeOn();
			}
		}

		// The ZRC SDK speaker volume float is on a 0-255 scale (confirmed on hardware .116, and matches
		// the SDK's far-end audio volume range [0,255]); the Essentials slider is 0-65535. Both
		// conversions live here. The SDK has no discrete output-mute, so MuteOn/Off set/restore the volume.
		private const float SdkSpeakerVolumeMax = 255f;
		private const ushort VolumeStep = 3277; // ~5% of 65535 per VolumeUp/Down press

		private static float LevelToSdkVolume(ushort level) => level / 65535f * SdkSpeakerVolumeMax;

		public override void MuteOff()
		{
			if (!_sdkSpeakerMuted) return;
			_sdkSpeakerMuted = false;
			_controller.SetSpeakerVolume(LevelToSdkVolume(_sdkSpeakerVolumeLevel));
			MuteFeedback.FireUpdate();
		}

		public override void MuteOn()
		{
			if (_sdkSpeakerMuted) return;
			_sdkSpeakerMuted = true;
			_controller.SetSpeakerVolume(0f); // no discrete SDK output-mute; zero the volume, restore on unmute
			MuteFeedback.FireUpdate();
		}

		public override void MuteToggle()
		{
			if (MuteFeedback.BoolValue)
			{
				MuteOff();
			}
			else
			{
				MuteOn();
			}
		}


		/// <summary>
		/// Increments the voluem
		/// </summary>
		/// <param name="pressRelease"></param>
		public override void VolumeUp(bool pressRelease)
		{
			if (!pressRelease) return; // act on press; continuous press-hold ramp could be added with a timer
			SetVolume((ushort)Math.Min(ushort.MaxValue, _sdkSpeakerVolumeLevel + VolumeStep));
		}

		/// <summary>
		/// Decrements the volume
		/// </summary>
		/// <param name="pressRelease"></param>
		public override void VolumeDown(bool pressRelease)
		{
			if (!pressRelease) return;
			SetVolume((ushort)Math.Max(0, _sdkSpeakerVolumeLevel - VolumeStep));
		}

		/// <summary>
		/// Scales the level and sets the codec to the specified level within its range
		/// </summary>
		/// <param name="level">level from slider (0-65535 range)</param>
		public override void SetVolume(ushort level)
		{
			_sdkSpeakerVolumeLevel = level;
			_sdkSpeakerMuted = false; // actively setting volume clears mute
			_controller.SetSpeakerVolume(LevelToSdkVolume(level));
			VolumeLevelFeedback.FireUpdate();
			MuteFeedback.FireUpdate();
		}

		/// <summary>
		/// Recalls the default volume on the codec
		/// </summary>
		public void VolumeSetToDefault()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		public override void StandbyActivate()
		{
			// No corresponding function on device
		}

		/// <summary>
		/// 
		/// </summary>
		public override void StandbyDeactivate()
		{
			// No corresponding function on device
		}

		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new ZoomRoomJoinMap(joinStart);

			var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

			if (customJoins != null)
			{
				joinMap.SetCustomJoinData(customJoins);
			}

			if (bridge != null)
			{
				bridge.AddJoinMap(Key, joinMap);
			}

			LinkVideoCodecToApi(this, trilist, joinMap);

			LinkZoomRoomToApi(trilist, joinMap);
		}

		/// <summary>
		/// Links all the specific Zoom functionality to the API bridge
		/// </summary>
		/// <param name="trilist"></param>
		/// <param name="joinMap"></param>
		public void LinkZoomRoomToApi(BasicTriList trilist, ZoomRoomJoinMap joinMap)
		{
            var meetingInfoCodec = this as IHasMeetingInfo;
            if (meetingInfoCodec != null)
            {
                if (meetingInfoCodec.MeetingInfo != null)
                {
                    trilist.SetBool(joinMap.MeetingCanRecord.JoinNumber, meetingInfoCodec.MeetingInfo.CanRecord);
                }

                meetingInfoCodec.MeetingInfoChanged += (o, a) =>
                    {
                        trilist.SetBool(joinMap.MeetingCanRecord.JoinNumber, a.Info.CanRecord);
                    };
            }

            var recordingCodec = this as IHasMeetingRecordingWithPrompt;
            if (recordingCodec != null)
            {
                trilist.SetSigFalseAction(joinMap.StartRecording.JoinNumber, () => recordingCodec.StartRecording());
                trilist.SetSigFalseAction(joinMap.StopRecording.JoinNumber, () => recordingCodec.StopRecording());

                recordingCodec.MeetingIsRecordingFeedback.LinkInputSig(trilist.BooleanInput[joinMap.StartRecording.JoinNumber]);
                recordingCodec.MeetingIsRecordingFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.StopRecording.JoinNumber]);

                trilist.SetSigFalseAction(joinMap.RecordingPromptAgree.JoinNumber, () => recordingCodec.RecordingPromptAcknowledgement(true));
                trilist.SetSigFalseAction(joinMap.RecordingPromptDisagree.JoinNumber, () => recordingCodec.RecordingPromptAcknowledgement(false));

                recordingCodec.RecordConsentPromptIsVisible.LinkInputSig(trilist.BooleanInput[joinMap.RecordConsentPromptIsVisible.JoinNumber]);
            }

			var layoutsCodec = this as IHasZoomRoomLayouts;
			if (layoutsCodec != null)
			{
				layoutsCodec.LayoutInfoChanged += (o, a) =>
				{
					trilist.SetBool(joinMap.LayoutGalleryIsAvailable.JoinNumber, 
                        zConfiguration.eLayoutStyle.Gallery == (a.AvailableLayouts & zConfiguration.eLayoutStyle.Gallery));

					trilist.SetBool(joinMap.LayoutSpeakerIsAvailable.JoinNumber, 
                        zConfiguration.eLayoutStyle.Speaker == (a.AvailableLayouts & zConfiguration.eLayoutStyle.Speaker));
					                                                             
					                                                             
					                                                              
					trilist.SetBool(joinMap.LayoutStripIsAvailable.JoinNumber, zConfiguration.eLayoutStyle.Strip
					                                                           ==
					                                                           (a.AvailableLayouts & zConfiguration.eLayoutStyle.Strip));
					trilist.SetBool(joinMap.LayoutShareAllIsAvailable.JoinNumber, zConfiguration.eLayoutStyle.ShareAll
					                                                              ==
					                                                              (a.AvailableLayouts &
					                                                               zConfiguration.eLayoutStyle.ShareAll));

					// pass the names used to set the layout through the bridge
					trilist.SetString(joinMap.LayoutGalleryIsAvailable.JoinNumber, zConfiguration.eLayoutStyle.Gallery.ToString());
					trilist.SetString(joinMap.LayoutSpeakerIsAvailable.JoinNumber, zConfiguration.eLayoutStyle.Speaker.ToString());
					trilist.SetString(joinMap.LayoutStripIsAvailable.JoinNumber, zConfiguration.eLayoutStyle.Strip.ToString());
					trilist.SetString(joinMap.LayoutShareAllIsAvailable.JoinNumber, zConfiguration.eLayoutStyle.ShareAll.ToString());
				};

				trilist.SetSigFalseAction(joinMap.SwapContentWithThumbnail.JoinNumber, () => layoutsCodec.SwapContentWithThumbnail());

				layoutsCodec.CanSwapContentWithThumbnailFeedback.LinkInputSig(
					trilist.BooleanInput[joinMap.CanSwapContentWithThumbnail.JoinNumber]);
				layoutsCodec.ContentSwappedWithThumbnailFeedback.LinkInputSig(
					trilist.BooleanInput[joinMap.SwapContentWithThumbnail.JoinNumber]);

				layoutsCodec.LayoutViewIsOnFirstPageFeedback.LinkInputSig(
					trilist.BooleanInput[joinMap.LayoutIsOnFirstPage.JoinNumber]);
				layoutsCodec.LayoutViewIsOnLastPageFeedback.LinkInputSig(trilist.BooleanInput[joinMap.LayoutIsOnLastPage.JoinNumber]);
				trilist.SetSigFalseAction(joinMap.LayoutTurnToNextPage.JoinNumber, () => layoutsCodec.LayoutTurnNextPage());
				trilist.SetSigFalseAction(joinMap.LayoutTurnToPreviousPage.JoinNumber, () => layoutsCodec.LayoutTurnPreviousPage());
				trilist.SetSigFalseAction(joinMap.GetAvailableLayouts.JoinNumber, () => layoutsCodec.GetAvailableLayouts());

				trilist.SetStringSigAction(joinMap.GetSetCurrentLayout.JoinNumber, (s) =>
				{
					try
					{
						var style = (zConfiguration.eLayoutStyle) Enum.Parse(typeof (zConfiguration.eLayoutStyle), s, true);
						SetLayout(style);
					}
					catch (Exception e)
					{
						this.LogInformation("Unable to parse '{LayoutStyleString}' to zConfiguration.eLayoutStyle: {Exception}", s, e);
					}
				});

				layoutsCodec.LocalLayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.GetSetCurrentLayout.JoinNumber]);
			}

			var pinCodec = this as IHasParticipantPinUnpin;
			if (pinCodec != null)
			{
				pinCodec.NumberOfScreensFeedback.LinkInputSig(trilist.UShortInput[joinMap.NumberOfScreens.JoinNumber]);

				// Set the value of the local property to be used when pinning a participant
				trilist.SetUShortSigAction(joinMap.ScreenIndexToPinUserTo.JoinNumber, (u) => ScreenIndexToPinUserTo = u);
			}

			var layoutSizeCodec = this as IHasSelfviewSize;
			if (layoutSizeCodec != null)
			{
				trilist.SetSigFalseAction(joinMap.GetSetSelfviewPipSize.JoinNumber, layoutSizeCodec.SelfviewPipSizeToggle);
				trilist.SetStringSigAction(joinMap.GetSetSelfviewPipSize.JoinNumber, (s) =>
				{
					try
					{
						var size = (zConfiguration.eLayoutSize) Enum.Parse(typeof (zConfiguration.eLayoutSize), s, true);
						var cmd = SelfviewPipSizes.FirstOrDefault(c => c.Command.Equals(size.ToString()));
						SelfviewPipSizeSet(cmd);
					}
					catch (Exception e)
					{
						this.LogInformation("Unable to parse '{LayoutSizeString}' to zConfiguration.eLayoutSize: {Exception}", s, e);
					}
				});

				layoutSizeCodec.SelfviewPipSizeFeedback.LinkInputSig(trilist.StringInput[joinMap.GetSetSelfviewPipSize.JoinNumber]);
			}		    

		    MeetingInfoChanged += (device, args) =>
		    {
                trilist.SetString(joinMap.MeetingInfoId.JoinNumber, args.Info.Id);
                trilist.SetString(joinMap.MeetingInfoHost.JoinNumber, args.Info.Host);
                trilist.SetString(joinMap.MeetingInfoPassword.JoinNumber, args.Info.Password);
		        trilist.SetBool(joinMap.IsHost.JoinNumber, args.Info.IsHost);
		        trilist.SetBool(joinMap.ShareOnlyMeeting.JoinNumber, args.Info.IsSharingMeeting);
                trilist.SetBool(joinMap.WaitingForHost.JoinNumber, args.Info.WaitingForHost);
                //trilist.SetString(joinMap.CurrentSource.JoinNumber, args.Info.ShareStatus);
		    };

		    trilist.SetSigFalseAction(joinMap.StartMeetingNow.JoinNumber, () => StartMeeting(0));
            trilist.SetSigFalseAction(joinMap.ShareOnlyMeeting.JoinNumber, StartSharingOnlyMeeting);
            trilist.SetSigFalseAction(joinMap.StartNormalMeetingFromSharingOnlyMeeting.JoinNumber, StartNormalMeetingFromSharingOnlyMeeting);

			trilist.SetStringSigAction(joinMap.SubmitPassword.JoinNumber, SubmitPassword);

            // Subscribe to call status to clear ShowPasswordPrompt when in meeting
            this.CallStatusChange += (o, a) =>
                {
                    if (a.CallItem.Status == eCodecCallStatus.Connected || a.CallItem.Status == eCodecCallStatus.Disconnected)
                    {
                        trilist.SetBool(joinMap.MeetingPasswordRequired.JoinNumber, false);
                    }

                };

            trilist.SetSigFalseAction(joinMap.CancelJoinAttempt.JoinNumber, () => {
                trilist.SetBool(joinMap.MeetingPasswordRequired.JoinNumber, false);
                EndAllCalls();
            });

			PasswordRequired += (devices, args) =>
			{
                this.LogDebug("***********************************PaswordRequired. Message: {Message} Cancelled: {Cancelled} Last Incorrect: {LastIncorrect} Failed: {Failed}", args.Message, args.LoginAttemptCancelled, args.LastAttemptWasIncorrect, args.LoginAttemptFailed);

				if (args.LoginAttemptCancelled)
				{
					trilist.SetBool(joinMap.MeetingPasswordRequired.JoinNumber, false);
					return;
				}

				if (!string.IsNullOrEmpty(args.Message))
				{
					trilist.SetString(joinMap.PasswordPromptMessage.JoinNumber, args.Message);
				}

				if (args.LoginAttemptFailed)
				{
					// login attempt failed
					return;
				}

				trilist.SetBool(joinMap.PasswordIncorrect.JoinNumber, args.LastAttemptWasIncorrect);
				trilist.SetBool(joinMap.MeetingPasswordRequired.JoinNumber, true);
			};

			trilist.OnlineStatusChange += (device, args) =>
			{
				if (!args.DeviceOnLine) return;

				ComputeAvailableLayouts();
				layoutsCodec.LocalLayoutFeedback.FireUpdate();
				layoutsCodec.CanSwapContentWithThumbnailFeedback.FireUpdate();
				layoutsCodec.ContentSwappedWithThumbnailFeedback.FireUpdate();
				layoutsCodec.LayoutViewIsOnFirstPageFeedback.FireUpdate();
				layoutsCodec.LayoutViewIsOnLastPageFeedback.FireUpdate();
				pinCodec.NumberOfScreensFeedback.FireUpdate();
				layoutSizeCodec.SelfviewPipSizeFeedback.FireUpdate();
			};

            var wirelessInfoCodec = this as IZoomWirelessShareInstructions;
            if (wirelessInfoCodec != null)
            {
                if (Status != null && Status.Sharing != null)
                {
                    SetSharingStateJoins(Status.Sharing, trilist, joinMap);
                }

                wirelessInfoCodec.ShareInfoChanged += (o, a) =>
                    {
                        SetSharingStateJoins(a.SharingStatus, trilist, joinMap);
                    };
            }
		}

        void SetSharingStateJoins(zStatus.Sharing state, BasicTriList trilist, ZoomRoomJoinMap joinMap)
        {
            trilist.SetBool(joinMap.IsSharingAirplay.JoinNumber, state.isAirHostClientConnected);
            trilist.SetBool(joinMap.IsSharingHdmi.JoinNumber, state.isBlackMagicConnected || state.isDirectPresentationConnected);
            
            trilist.SetString(joinMap.DisplayState.JoinNumber, state.dispState.ToString());
            trilist.SetString(joinMap.AirplayShareCode.JoinNumber, state.password);
            trilist.SetString(joinMap.LaptopShareKey.JoinNumber, state.directPresentationSharingKey);
            trilist.SetString(joinMap.WifiName.JoinNumber, state.wifiName);
            trilist.SetString(joinMap.ServerName.JoinNumber, state.serverName);
        }

		public override void ExecuteSwitch(object selector)
		{
			var action = selector as Action;
			if (action == null)
			{
				return;
			}

			action();
		}

		public void AcceptCall()
		{
			var incomingCall =
				ActiveCalls.FirstOrDefault(
					c => c.Status.Equals(eCodecCallStatus.Ringing) && c.Direction.Equals(eCodecCallDirection.Incoming));

			if (incomingCall != null)
				AcceptCall(incomingCall);
		}

		public override void AcceptCall(CodecActiveCallItem call)
		{
			if (call != null)
				_controller.JoinMeeting(call.Id);
		}

		public void RejectCall()
		{
			var incomingCall =
				ActiveCalls.FirstOrDefault(
					c => c.Status.Equals(eCodecCallStatus.Ringing) && c.Direction.Equals(eCodecCallDirection.Incoming));

			if (incomingCall != null)
				RejectCall(incomingCall);
		}

		public override void RejectCall(CodecActiveCallItem call)
		{
			// Decline the incoming meeting invite via the SDK (it answers the cached invite with
			// accept=false). The local call-status update keeps the UI in sync regardless.
			_controller.AnswerMeetingInvite(false);
			if (call != null)
			{
				call.Status = eCodecCallStatus.Disconnected;
				OnCallStatusChange(call);
			}
		}

		public override void Dial(Meeting meeting)
		{
			if (!EnsureConnected("Dial(meeting)")) return;
			this.LogInformation("Dialing meeting.Id: {MeetingId} Title: {MeetingTitle}", meeting.Id, meeting.Title);
			_controller.JoinMeeting(meeting.Id);
		}

		public override void Dial(string number)
		{
			if (!EnsureConnected("Dial(number)")) return;
			this.LogDebug("Dialing number: {Number}", number);
			_controller.JoinMeeting(number);
		}

        /// <summary>
        /// Dials a meeting with a password
        /// </summary>
        public void Dial(string number, string password)
        {
            if (!EnsureConnected("Dial(number,password)")) return;
            this.LogDebug("Dialing meeting number: {Number} with password: {Password}", number, password);
            _controller.JoinMeetingWithPassword(number, password);
        }

		/// <summary>
		/// Invites a contact to either a new meeting (if not already in a meeting) or the current meeting.
		/// Currently only invites a single user
		/// </summary>
		/// <param name="contact"></param>
		public override void Dial(IInvitableContact contact)
		{
			if (!EnsureConnected("Dial(contact)")) return;

            var ic = contact as InvitableDirectoryContact;

			if (ic == null || string.IsNullOrEmpty(ic.ContactId))
			{
				this.LogWarning("Dial(IInvitableContact): contact has no ContactId");
				return;
			}

			var contactIds = new[] { ic.ContactId };
			if (IsInCall)
			{
				this.LogInformation("Inviting contact {ContactId} to current meeting", ic.ContactId);
				_controller.InviteAttendees(contactIds);
			}
			else
			{
				this.LogInformation("Starting new meeting with contact {ContactId}", ic.ContactId);
				_controller.MeetWithImUsers(contactIds);
			}
		}

        /// <summary>
        /// Invites contacts to a new meeting for a specified duration
        /// </summary>
        /// <param name="contacts"></param>
        /// <param name="duration"></param>
        public void InviteContactsToNewMeeting(List<InvitableDirectoryContact> contacts, uint duration)
        {
            if (!EnsureConnected("InviteContactsToNewMeeting")) return;
            var contactIds = GetContactIds(contacts);
            if (contactIds.Length == 0)
            {
                this.LogWarning("InviteContactsToNewMeeting: no valid contact IDs");
                return;
            }

            this.LogInformation("Starting new meeting with {Count} contact(s)", contactIds.Length);
            _controller.MeetWithImUsers(contactIds);
        }

        /// <summary>
        /// Invites contacts to an existing meeting
        /// </summary>
        /// <param name="contacts"></param>
        public void InviteContactsToExistingMeeting(List<InvitableDirectoryContact> contacts)
        {
            if (!EnsureConnected("InviteContactsToExistingMeeting")) return;
            var contactIds = GetContactIds(contacts);
            if (contactIds.Length == 0)
            {
                this.LogWarning("InviteContactsToExistingMeeting: no valid contact IDs");
                return;
            }

            this.LogInformation("Inviting {Count} contact(s) to current meeting", contactIds.Length);
            _controller.InviteAttendees(contactIds);
        }

        // Extracts the non-empty contact IDs from a list of invitable contacts.
        private static string[] GetContactIds(List<InvitableDirectoryContact> contacts)
        {
            if (contacts == null) return Array.Empty<string>();
            return contacts
                .Where(c => c != null && !string.IsNullOrEmpty(c.ContactId))
                .Select(c => c.ContactId)
                .ToArray();
        }


        /// <summary>
        /// Starts a PMI Meeting for the specified duration (or default meeting duration if 0 is specified)
        /// </summary>
        /// <param name="duration">duration of meeting</param>
        public void StartMeeting(uint duration)
        {
            if (!EnsureConnected("StartMeeting")) return;
            _controller.StartInstantMeeting();
        }

        public void LeaveMeeting()
        {
			_meetingPasswordRequired = false;
			_controller.LeaveMeeting();
        }

		/// <summary>
		/// Ends the current meeting for all participants (host action), as opposed to <see cref="LeaveMeeting"/>
		/// which only removes this Zoom Room from the meeting.
		/// </summary>
		public void EndMeetingForAll()
		{
			_meetingPasswordRequired = false;
			_controller.EndMeeting();
		}

		public override void EndCall(CodecActiveCallItem call)
		{
			_meetingPasswordRequired = false;
			_controller.LeaveMeeting();
		}

		public override void EndAllCalls()
		{
			_meetingPasswordRequired = false;
			_controller.LeaveMeeting();
		}

		public override void SendDtmf(string s)
		{
			SendDtmfToPhone(s);
		}

		// Maps a batch of SDK contacts into the accumulated directory, rebuilds DirectoryRoot and
		// publishes the result. The subscription delivers contacts in pages, so batches are merged
		// by contact ID rather than replacing the whole directory each time.
		private void OnControllerContactListChanged(object sender, ContactListEventArgs e)
		{
			if (e == null || e.Contacts == null) return;

			CodecDirectory directory;
			lock (_directoryLock)
			{
				foreach (var c in e.Contacts)
				{
					if (string.IsNullOrEmpty(c.ContactID)) continue;
					_directoryContactsById[c.ContactID] = c;
				}

				directory = new CodecDirectory { ResultsFolderId = "root" };
				directory.AddContactsToDirectory(
					_directoryContactsById.Values.Select(c => (DirectoryItem)MapDirectoryContact(c)).ToList());
			}

			this.LogInformation("Directory updated: {ContactCount} contact(s) (batch of {BatchCount})",
				directory.Contacts.Count, e.Contacts.Length);

			DirectoryRoot = directory;

			PhonebookSyncState.SetPhonebookHasFolders(false);
			PhonebookSyncState.InitialPhonebookFoldersReceived();
			PhonebookSyncState.PhonebookRootEntriesReceived();
			PhonebookSyncState.SetNumberOfContacts(directory.Contacts.Count);

			// Refresh the current view if the user is browsing the root.
			if (CurrentDirectoryResult == null || CurrentDirectoryResult.ResultsFolderId == "root")
			{
				CurrentDirectoryResult = DirectoryRoot;
			}
		}

		// Maps a single SDK contact to an Essentials invitable directory contact (flat, parented to root).
		private static InvitableDirectoryContact MapDirectoryContact(ContactInfo c)
		{
			var name = !string.IsNullOrEmpty(c.ScreenName)
				? c.ScreenName
				: string.Join(" ", new[] { c.FirstName, c.LastName }.Where(s => !string.IsNullOrEmpty(s)));

			var contact = new InvitableDirectoryContact
			{
				Name = string.IsNullOrEmpty(name) ? c.ContactID : name,
				ContactId = c.ContactID,
				ParentFolderId = "root",
			};

			contact.ContactMethods.Add(new ContactMethod
			{
				Number = c.ContactID,
				Device = eContactMethodDevice.Video,
				CallType = eContactMethodCallType.Video,
				ContactMethodId = c.ContactID,
			});

			return contact;
		}

		// Maps the SDK schedule (bookings) into the Essentials CodecScheduleAwareness model and
		// publishes it. The SDK delivers the full list on each update, so the meeting list is
		// replaced wholesale rather than merged.
		private void OnControllerMeetingListChanged(object sender, MeetingListEventArgs e)
		{
			if (e == null || e.Meetings == null) return;

			var meetings = e.Meetings
				.Select(MapMeeting)
				.Where(m => m != null)
				.ToList();

			this.LogInformation("Schedule updated: {MeetingCount} meeting(s) (result {Result})",
				meetings.Count, e.Result);

			CodecSchedule.Meetings = meetings;
		}

		// Maps a single SDK meeting item to an Essentials Meeting. Returns null if start/end times
		// cannot be parsed, since the schedule model relies on them for joinable/warning logic.
		private Meeting MapMeeting(MeetingItemInfo item)
		{
			if (item == null) return null;

			if (!DateTime.TryParse(item.StartTime, CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind, out var startTime) ||
				!DateTime.TryParse(item.EndTime, CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind, out var endTime))
			{
				this.LogDebug("Skipping meeting {MeetingNumber}: unparseable start/end time ('{Start}' / '{End}')",
					item.MeetingNumber, item.StartTime, item.EndTime);
				return null;
			}

			return new Meeting
			{
				Id = item.MeetingNumber,
				Title = item.MeetingName,
				Organizer = item.HostName,
				StartTime = startTime,
				EndTime = endTime,
				Privacy = item.IsPrivate ? eMeetingPrivacy.Private : eMeetingPrivacy.Public,
				Dialable = !string.IsNullOrEmpty(item.MeetingNumber),
				MinutesBeforeMeeting = CodecSchedule.MeetingWarningMinutes,
			};
		}

		/// <summary>
		/// Call when directory results are updated
		/// </summary>
		/// <param name="result"></param>
		private void OnDirectoryResultReturned(CodecDirectory result)
		{
			try
			{
				this.LogDebug("OnDirectoryResultReturned.  Result has {ContactCount} contacts", result.Contacts.Count);

				CurrentDirectoryResultIsNotDirectoryRoot.FireUpdate();

                var directoryResult = result;
				var directoryIsRoot = CurrentDirectoryResultIsNotDirectoryRoot.BoolValue == false;

				// If result is Root, create a copy and filter out contacts whose parent folder is not root
                //if (!CurrentDirectoryResultIsNotDirectoryRoot.BoolValue)
                //{
                //    Debug.Console(2, this, "Filtering DirectoryRoot to remove contacts for display");

                //    directoryResult.ResultsFolderId = result.ResultsFolderId;
                //    directoryResult.AddFoldersToDirectory(result.Folders);
                //    directoryResult.AddContactsToDirectory(
                //        result.Contacts.Where((c) => c.ParentFolderId == result.ResultsFolderId).ToList());
                //}
                //else
                //{
                //    directoryResult = result;
                //}

				this.LogDebug("Updating directoryResult. IsOnRoot: {DirectoryIsRoot} Contact Count: {ContactCount}",
					directoryIsRoot, directoryResult.Contacts.Count);

				// This will return the latest results to all UIs.  Multiple indendent UI Directory browsing will require a different methodology
				var handler = DirectoryResultReturned;
				if (handler != null)
				{
					handler(this, new DirectoryEventArgs
					{
						Directory = directoryResult,
						DirectoryIsOnRoot = directoryIsRoot
					});
				}

                
			}
			catch (Exception e)
			{
				this.LogDebug("Error: {Exception}", e);
			}

			//PrintDirectory(result);
		}

		/// <summary>
		/// Builds the cameras List by using the Zoom Room zStatus.Cameras data.  Could later be modified to build from config data
		/// </summary>
		private void SetUpCameras()
		{
			// Near-end cameras come from the SDK's setting-service device list (real device IDs).
			// There is no SDK push for camera-list/selection changes, so this is refreshed on connect
			// and after a SelectCamera; external (Zoom UI) camera swaps won't reflect until then.
			var devices = _controller.GetCameras();
			if (devices == null || devices.Length == 0)
			{
				this.LogInformation("No local cameras reported by the SDK");
			}
			else
			{
				// Mutate the shared Cameras list under _participantLock; UpdateFarEndCameras (on the
				// participant thread) and SelectCamera (on the UI thread) also serialize on this lock.
				IHasCameraControls cameraToSelect = null;
				lock (_participantLock)
				{
					foreach (var dev in devices)
					{
						// Crestron UC engine systems report the USB bridge device in the camera list; skip it.
						if (!string.IsNullOrEmpty(dev.Name) && dev.Name.IndexOf("HD-CONV-USB") > -1)
							continue;

						var existingCam = Cameras.FirstOrDefault((c) => c.Key.Equals(dev.Id));
						if (existingCam == null)
						{
							var displayName = string.IsNullOrEmpty(dev.DisplayName) ? dev.Name : dev.DisplayName;
							Cameras.Add(new ZoomRoomCamera(dev.Id, displayName, this));
							this.LogDebug("Added near-end camera id=\"{Id}\" name=\"{Name}\"", dev.Id, displayName);
						}

						if (dev.IsSelected)
						{
							var cam = Cameras.FirstOrDefault((c) => c.Key.Equals(dev.Id));
							if (cam != null) cameraToSelect = cam;
						}
					}
				}

				// Assign SelectedCamera outside the lock so its feedback/CameraSelected dispatch
				// doesn't run while _participantLock is held.
				if (cameraToSelect != null) SelectedCamera = cameraToSelect;
			}

			if (IsInCall)
			{
				UpdateFarEndCameras();
			}
		}

		/// <summary>
		/// Keeps <see cref="_participantInfoByUserId"/> in sync with the SDK participant events.
		/// Mirrors the maintenance applied to <c>Participants.CurrentParticipants</c> so far-end
		/// camera discovery can read the raw camera-control flags. Caller must hold <c>_participantLock</c>.
		/// </summary>
		private void TrackParticipantInfo(ParticipantInfo[] participants, bool fullReplace, bool isLeave)
		{
			if (fullReplace)
			{
				_participantInfoByUserId.Clear();
				foreach (var info in participants)
					_participantInfoByUserId[info.UserID] = info;
			}
			else if (isLeave)
			{
				foreach (var info in participants)
					_participantInfoByUserId.Remove(info.UserID);
			}
			else
			{
				foreach (var info in participants)
					_participantInfoByUserId[info.UserID] = info;
			}
		}

		/// <summary>
		/// Dynamically creates and removes <see cref="ZoomRoomFarEndCamera"/> instances for the
		/// remote participants whose camera the room is allowed to control. Control of an existing
		/// far-end camera is wired via <see cref="ControlFarEndCamera"/>; this reconciles the
		/// <c>Cameras</c> list against the current participant roster (driven by the SDK participant
		/// events). A participant is considered controllable when it is not the room itself and the
		/// SDK reports it can be requested for control (or the room is already controlling it).
		/// </summary>
		private void UpdateFarEndCameras()
		{
			lock (_participantLock)
			{
				var controllable = _participantInfoByUserId.Values
					.Where(info => !info.IsMySelf && (info.CameraCanRequestControl || info.CameraAmIControlling))
					.ToList();
				var controllableIds = new HashSet<int>(controllable.Select(info => info.UserID));

				// Add a camera for each newly-controllable participant.
				foreach (var info in controllable)
				{
					if (Cameras.OfType<ZoomRoomFarEndCamera>().Any(c => c.Id == info.UserID))
						continue;

					var key = string.Format("{0}-farEndCamera-{1}", Key, info.UserID);
					var name = string.IsNullOrEmpty(info.UserName)
						? string.Format("Far End Camera {0}", info.UserID)
						: info.UserName;
					Cameras.Add(new ZoomRoomFarEndCamera(key, name, this, info.UserID));
					this.LogDebug("Added far-end camera userId={UserId} name=\"{Name}\"", info.UserID, name);
				}

				// Remove cameras for participants who left or lost camera-control capability.
				var stale = Cameras.OfType<ZoomRoomFarEndCamera>()
					.Where(c => !(c.Id.HasValue && controllableIds.Contains(c.Id.Value)))
					.ToList();
				foreach (var cam in stale)
				{
					if (ReferenceEquals(SelectedCamera, cam))
						SelectedCamera = null;
					Cameras.Remove(cam);
					this.LogDebug("Removed far-end camera userId={UserId}", cam.Id);
				}
			}
		}

		/// <summary>
		/// Sends a far-end (participant) camera PTZ command to the SDK. The camera's <c>Id</c> is the
		/// target participant userID. Maps the plugin's camera enums to the ZRC SDK's
		/// CameraControlAction / CameraControlType ints.
		/// </summary>
		internal void ControlFarEndCamera(int userId, eZoomRoomCameraState state, eZoomRoomCameraAction action)
		{
			if (!TryMapCameraCommand(state, action, out var controlAction, out var controlType)) return;
			_controller.ControlUserCamera(userId, controlAction, controlType);
		}

		/// <summary>
		/// Sends a near-end PTZ command to a specific local camera by device ID. An empty device ID
		/// targets the room's main camera (the SDK's ControlLocalCamera convention).
		/// </summary>
		internal void ControlNearEndCamera(string deviceId, eZoomRoomCameraState state, eZoomRoomCameraAction action)
		{
			if (!TryMapCameraCommand(state, action, out var controlAction, out var controlType)) return;
			_controller.ControlCamera(deviceId ?? string.Empty, controlAction, controlType);
		}

		// Maps the plugin's camera enums to the ZRC SDK CameraControlAction / CameraControlType ints.
		private bool TryMapCameraCommand(eZoomRoomCameraState state, eZoomRoomCameraAction action, out int controlAction, out int controlType)
		{
			controlAction = -1; controlType = -1;
			switch (state)
			{
				case eZoomRoomCameraState.Start:    controlType = 0; break; // CameraControlTypeStart
				case eZoomRoomCameraState.Continue: controlType = 1; break; // CameraControlTypeContinue
				case eZoomRoomCameraState.Stop:     controlType = 2; break; // CameraControlTypeStop
				default:
					this.LogWarning("Camera command: unsupported state {State}", state);
					return false;
			}
			switch (action)
			{
				case eZoomRoomCameraAction.Up:    controlAction = 0; break; // CameraControlActionMoveUp
				case eZoomRoomCameraAction.Down:  controlAction = 1; break; // CameraControlActionMoveDown
				case eZoomRoomCameraAction.Left:  controlAction = 2; break; // CameraControlActionMoveLeft
				case eZoomRoomCameraAction.Right: controlAction = 3; break; // CameraControlActionMoveRight
				case eZoomRoomCameraAction.In:    controlAction = 4; break; // CameraControlActionZoomIn
				case eZoomRoomCameraAction.Out:   controlAction = 5; break; // CameraControlActionZoomOut
				default:
					this.LogWarning("Camera command: unsupported action {Action}", action);
					return false;
			}
			return true;
		}

		#region Implementation of IHasParticipants

		public CodecParticipants Participants { get; private set; }

        public void RemoveParticipant(int userId)
        {
            _controller.ExpelUser(userId);
        }

        public void SetParticipantAsHost(int userId)
        {
            _controller.AssignHost(userId);
        }

        public void AdmitParticipantFromWaitingRoom(int userId)
        {
            _controller.AdmitUserFromWaitingRoom(userId);
        }

        /// <summary>
        /// Console/test helper: logs the current participants and their userIds so per-participant
        /// commands (MuteVideoForParticipant, MuteAudioForParticipant, PinUser, etc.) can be exercised
        /// via devjson. Call with: devjson {"deviceKey":"...","methodName":"LogParticipants","params":[]}
        /// </summary>
        public void LogParticipants()
        {
            var list = Participants.CurrentParticipants;
            if (list == null || list.Count == 0)
            {
                this.LogInformation("Participants: (none — not in a meeting or list not yet populated)");
                return;
            }

            this.LogInformation("Participants ({Count}):", list.Count);
            foreach (var p in list)
            {
                this.LogInformation(
                    "  userId={UserId} name=\"{Name}\" host={IsHost} self={IsMyself} audioMuted={AudioMuted} videoMuted={VideoMuted} handRaised={HandRaised}",
                    p.UserId, p.Name, p.IsHost, p.IsMyself, p.AudioMuteFb, p.VideoMuteFb, p.HandIsRaisedFb);
            }
        }

		#endregion

		#region IHasParticipantAudioMute Members

        public void MuteAudioForAllParticipants()
        {
            _controller.MuteAllAudio(true);
        }

		public void MuteAudioForParticipant(int userId)
		{
			_controller.MuteUserAudio(userId, true);
		}

		public void UnmuteAudioForParticipant(int userId)
		{
			_controller.MuteUserAudio(userId, false);
		}

		public void ToggleAudioForParticipant(int userId)
		{
			var user = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId.Equals(userId));

			if (user == null)
			{
				this.LogDebug("Unable to find user with id: {UserId}", userId);
				return;
			}

			if (user.AudioMuteFb)
			{
				UnmuteAudioForParticipant(userId);
			}
			else
			{
				MuteAudioForParticipant(userId);
			}
		}

		#endregion

		#region IHasParticipantVideoMute Members

		public void MuteVideoForParticipant(int userId)
		{
			_controller.MuteUserVideo(userId, true);
		}

		public void UnmuteVideoForParticipant(int userId)
		{
			_controller.MuteUserVideo(userId, false);
		}

		public void ToggleVideoForParticipant(int userId)
		{
			var user = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId.Equals(userId));

			if (user == null)
			{
				this.LogDebug("Unable to find user with id: {UserId}", userId);
				return;
			}

			if (user.VideoMuteFb)
			{
				UnmuteVideoForParticipant(userId);
			}
			else
			{
				MuteVideoForParticipant(userId);
			}
		}

		#endregion

		#region IHasParticipantPinUnpin Members

		private Func<int> NumberOfScreensFeedbackFunc
		{
			get { return () => Status.NumberOfScreens.NumOfScreens; }
		}

		public IntFeedback NumberOfScreensFeedback { get; private set; }

		public int ScreenIndexToPinUserTo { get; private set; }

		public void PinParticipant(int userId, int screenIndex)
		{
			_controller.PinUserOnScreen(userId, screenIndex);
		}

		public void UnPinParticipant(int userId)
		{
			_controller.UnpinUserFromScreen(userId, 0);
		}

		public void ToggleParticipantPinState(int userId, int screenIndex)
		{
			var user = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId.Equals(userId));

			if (user == null)
			{
				this.LogDebug("Unable to find user with id: {UserId}", userId);
				return;
			}

			if (user.IsPinnedFb)
			{
				UnPinParticipant(userId);
			}
			else
			{
				PinParticipant(userId, screenIndex);
			}
		}

		#endregion

		#region Implementation of IHasCameraOff

		public BoolFeedback CameraIsOffFeedback { get; private set; }

		public void CameraOff()
		{
			CameraMuteOn();
		}

		#endregion

		public BoolFeedback CameraIsMutedFeedback { get; private set; }

		public void CameraMuteOn()
		{
			_sdkCameraOff = true;
			CameraIsOffFeedback.FireUpdate();
			_controller.SetVideoState(false);
		}

		public void CameraMuteOff()
		{
			_sdkCameraOff = false;
			CameraIsOffFeedback.FireUpdate();
			_controller.SetVideoState(true);
		}

		public void CameraMuteToggle()
		{
			if (CameraIsMutedFeedback.BoolValue)
				CameraMuteOff();
			else
				CameraMuteOn();
		}

		#region Implementation of IHasCameraAutoMode

		// SmartCameraMask: SpeakerFocus(2) = auto-framing follows the speaker; Manual(1) = no auto framing.
		private const int SmartCameraMaskManual = 1;
		private const int SmartCameraMaskSpeakerFocus = 2;

		// Smart-mode targets the selected near-end camera by device ID; empty falls back to the main camera.
		private string SelectedNearEndCameraDeviceId =>
			(_selectedCamera != null && !(_selectedCamera is IAmFarEndCamera)) ? _selectedCamera.Key : string.Empty;

		public void CameraAutoModeOn()
		{
			if (!_controller.ChangeSmartCameraMode(SmartCameraMaskSpeakerFocus, SelectedNearEndCameraDeviceId)) return;
			_cameraAutoModeOn = true;
			CameraAutoModeIsOnFeedback.FireUpdate();
		}

		public void CameraAutoModeOff()
		{
			if (!_controller.ChangeSmartCameraMode(SmartCameraMaskManual, SelectedNearEndCameraDeviceId)) return;
			_cameraAutoModeOn = false;
			CameraAutoModeIsOnFeedback.FireUpdate();
		}

		public void CameraAutoModeToggle()
		{
			if (_cameraAutoModeOn) CameraAutoModeOff();
			else CameraAutoModeOn();
		}

		public BoolFeedback CameraAutoModeIsOnFeedback { get; private set; }

		#endregion

		#region Implementation of IHasFarEndContentStatus

		public BoolFeedback ReceivingContent { get; private set; }

		#endregion

		#region Implementation of IHasSelfviewPosition

		private CodecCommandWithLabel _currentSelfviewPipPosition;

		public StringFeedback SelfviewPipPositionFeedback { get; private set; }

		public void SelfviewPipPositionSet(CodecCommandWithLabel position)
		{
			if (position == null) return;
			_currentSelfviewPipPosition = position;
			// ControlVideoPosition sets position AND size together, so supply the current size.
			_controller.ControlVideoPosition(SelfviewPositionToSdk(position.Command), CurrentSelfviewSizeSdk());
			SelfviewPipPositionFeedback.FireUpdate();
		}

		// Maps the plugin's self-view PiP position command -> ZRC SDK VideoThumbPosition int.
		private static int SelfviewPositionToSdk(string command)
		{
			switch ((command ?? string.Empty).ToLower())
			{
				case "upleft":    return 7; // VideoThumbPositionUpLeft
				case "upright":   return 3; // VideoThumbPositionUpRight
				case "downright": return 5; // VideoThumbPositionDownRight
				case "downleft":  return 8; // VideoThumbPositionDownLeft
				default:          return 3; // default UpRight
			}
		}

		// Maps the plugin's self-view PiP size command -> ZRC SDK VideoThumbSize int.
		private static int SelfviewSizeToSdk(string command)
		{
			switch ((command ?? string.Empty).ToLower())
			{
				case "off":   return 0; // VideoThumbSizeOff (hides the PiP)
				case "size1": return 1; // VideoThumbSize1x
				case "size2": return 2; // VideoThumbSize2x
				case "size3": return 3; // VideoThumbSize3x
				case "strip": return 4; // VideoThumbSizeVideoStripe
				default:      return 1; // default 1x
			}
		}

		private int CurrentSelfviewSizeSdk()
		{
			return _currentSelfviewPipSize != null ? SelfviewSizeToSdk(_currentSelfviewPipSize.Command) : 1;
		}

		private int CurrentSelfviewPositionSdk()
		{
			return _currentSelfviewPipPosition != null ? SelfviewPositionToSdk(_currentSelfviewPipPosition.Command) : 3;
		}

		public void SelfviewPipPositionToggle()
		{
			if (_currentSelfviewPipPosition != null)
			{
				var nextPipPositionIndex = SelfviewPipPositions.IndexOf(_currentSelfviewPipPosition) + 1;

				if (nextPipPositionIndex >= SelfviewPipPositions.Count)
					// Check if we need to loop back to the first item in the list
					nextPipPositionIndex = 0;

				SelfviewPipPositionSet(SelfviewPipPositions[nextPipPositionIndex]);
			}
		}

		public List<CodecCommandWithLabel> SelfviewPipPositions = new List<CodecCommandWithLabel>()
		{
			new CodecCommandWithLabel("UpLeft", "Center Left"),
			new CodecCommandWithLabel("UpRight", "Center Right"),
			new CodecCommandWithLabel("DownRight", "Lower Right"),
			new CodecCommandWithLabel("DownLeft", "Lower Left")
		};

		private void ComputeSelfviewPipPositionStatus()
		{
			_currentSelfviewPipPosition =
				SelfviewPipPositions.FirstOrDefault(
					p => p.Command.ToLower().Equals(Configuration.Call.Layout.Position.ToString().ToLower()));
		}

		#endregion

		// TODO: #714 [ ] Implementation of IHasSelfviewPipSize

		#region Implementation of IHasSelfviewPipSize

		private CodecCommandWithLabel _currentSelfviewPipSize;

		// Last non-Off size, so SelfViewModeOn can restore the size the user last chose.
		private CodecCommandWithLabel _lastVisibleSelfviewPipSize;

		public StringFeedback SelfviewPipSizeFeedback { get; private set; }

		public void SelfviewPipSizeSet(CodecCommandWithLabel size)
		{
			if (size == null) return;
			_currentSelfviewPipSize = size;
			if (!"Off".Equals(size.Command, StringComparison.OrdinalIgnoreCase))
				_lastVisibleSelfviewPipSize = size;
			// ControlVideoPosition sets size AND position together, so supply the current position.
			_controller.ControlVideoPosition(CurrentSelfviewPositionSdk(), SelfviewSizeToSdk(size.Command));
			SelfviewPipSizeFeedback.FireUpdate();
			SelfviewIsOnFeedback.FireUpdate(); // Off vs non-Off changes the self-view-on state
		}

		public void SelfviewPipSizeToggle()
		{
			if (_currentSelfviewPipSize != null)
			{
				var nextPipSizeIndex = SelfviewPipSizes.IndexOf(_currentSelfviewPipSize) + 1;

				if (nextPipSizeIndex >= SelfviewPipSizes.Count)
					// Check if we need to loop back to the first item in the list
					nextPipSizeIndex = 0;

				SelfviewPipSizeSet(SelfviewPipSizes[nextPipSizeIndex]);
			}
		}

		public List<CodecCommandWithLabel> SelfviewPipSizes = new List<CodecCommandWithLabel>()
		{
			new CodecCommandWithLabel("Off", "Off"),
			new CodecCommandWithLabel("Size1", "Size 1"),
			new CodecCommandWithLabel("Size2", "Size 2"),
			new CodecCommandWithLabel("Size3", "Size 3"),
			new CodecCommandWithLabel("Strip", "Strip")
		};

		private void ComputeSelfviewPipSizeStatus()
		{
			_currentSelfviewPipSize =
				SelfviewPipSizes.FirstOrDefault(
					p => p.Command.ToLower().Equals(Configuration.Call.Layout.Size.ToString().ToLower()));
		}

		#endregion

		#region Implementation of IHasPhoneDialing

		private Func<bool> PhoneOffHookFeedbackFunc
		{
			get { return () => Status.PhoneCall.OffHook; }
		}

		private Func<string> CallerIdNameFeedbackFunc
		{
			get { return () => Status.PhoneCall.PeerDisplayName; }
		}

		private Func<string> CallerIdNumberFeedbackFunc
		{
			get { return () => Status.PhoneCall.PeerNumber; }
		}

		public BoolFeedback PhoneOffHookFeedback { get; private set; }
		public StringFeedback CallerIdNameFeedback { get; private set; }
		public StringFeedback CallerIdNumberFeedback { get; private set; }

		public void DialPhoneCall(string number)
		{
			// SIP is the default transport; PSTN call-out adds the number to the CURRENT meeting
			// (the SDK's third-party-meeting helper has no standalone dial-out), so it only does
			// something when the room is already in a meeting.
			if (_props != null && _props.PhoneDialMode == ePhoneDialMode.Pstn)
			{
				_controller.CallOutPstnUser(number, false, false);
				return;
			}
			_controller.CallSip(number);
		}

		public void EndPhoneCall()
		{
			_controller.TerminateSipCall(_activeSipCallId);
		}

		public void SendDtmfToPhone(string digit)
		{
			// Empty callId targets the single active SIP call.
			_controller.SendDtmfToSipCall(digit, _activeSipCallId ?? string.Empty);
		}

		#endregion

		#region IHasZoomRoomLayouts Members

		public event EventHandler<LayoutInfoChangedEventArgs> LayoutInfoChanged;

		private Func<bool> LayoutViewIsOnFirstPageFeedbackFunc
		{
			get { return () => _layoutIsOnFirstPage; } // driven by the SDK VideoPageStatus notification
		}

		private Func<bool> LayoutViewIsOnLastPageFeedbackFunc
		{
			get { return () => _layoutIsOnLastPage; } // driven by the SDK VideoPageStatus notification
		}

		private Func<bool> CanSwapContentWithThumbnailFeedbackFunc
		{
			get { return () => Status.Layout.can_Switch_Floating_Share_Content; }
		}

		private Func<bool> ContentSwappedWithThumbnailFeedbackFunc
		{
			get { return () => _contentSwappedWithThumbnail; }
		}

		public BoolFeedback LayoutViewIsOnFirstPageFeedback { get; private set; }

		public BoolFeedback LayoutViewIsOnLastPageFeedback { get; private set; }

		public BoolFeedback CanSwapContentWithThumbnailFeedback { get; private set; }

		public BoolFeedback ContentSwappedWithThumbnailFeedback { get; private set; }


		public zConfiguration.eLayoutStyle LastSelectedLayout { get; private set; }

		public zConfiguration.eLayoutStyle AvailableLayouts { get; private set; }

		/// <summary>
		/// Reads individual properties to determine if which layouts are avalailable
		/// </summary>
		private void ComputeAvailableLayouts()
		{
			this.LogInformation("Computing available layouts...");
			zConfiguration.eLayoutStyle availableLayouts = zConfiguration.eLayoutStyle.None;
			if (Status.Layout.can_Switch_Wall_View)
			{
				availableLayouts |= zConfiguration.eLayoutStyle.Gallery;
			}

			if (Status.Layout.can_Switch_Speaker_View)
			{
				availableLayouts |= zConfiguration.eLayoutStyle.Speaker;
			}

			if (Status.Layout.can_Switch_Share_On_All_Screens)
			{
				availableLayouts |= zConfiguration.eLayoutStyle.ShareAll;
			}

			// There is no property that directly reports if strip mode is valid, but API stipulates
			// that strip mode is available if the number of screens is 1
			if (Status.NumberOfScreens.NumOfScreens == 1 || Status.Layout.can_Switch_Strip_View || Status.Layout.video_type.ToLower() == "strip")
			{
				availableLayouts |= zConfiguration.eLayoutStyle.Strip;
			}

			this.LogInformation("availablelayouts: {AvailableLayouts}", availableLayouts);

			AvailableLayouts = availableLayouts;
		}

        private void OnLayoutInfoChanged()
        {
            var handler = LayoutInfoChanged;
            if (handler != null)
            {

                var currentLayout = zConfiguration.eLayoutStyle.None;

                currentLayout = (zConfiguration.eLayoutStyle)Enum.Parse(typeof(zConfiguration.eLayoutStyle), string.IsNullOrEmpty(LocalLayoutFeedback.StringValue) ? "None" : LocalLayoutFeedback.StringValue, true);            

                handler(this, new LayoutInfoChangedEventArgs()
                {
                    AvailableLayouts = AvailableLayouts,
                    CurrentSelectedLayout = currentLayout,
                    LayoutViewIsOnFirstPage = LayoutViewIsOnFirstPageFeedback.BoolValue,
                    LayoutViewIsOnLastPage = LayoutViewIsOnLastPageFeedback.BoolValue,
                    CanSwapContentWithThumbnail = CanSwapContentWithThumbnailFeedback.BoolValue,
                    ContentSwappedWithThumbnail = ContentSwappedWithThumbnailFeedback.BoolValue,
                });
            }
        }

		public void GetAvailableLayouts()
		{
			ComputeAvailableLayouts();
		}

		public void SetLayout(zConfiguration.eLayoutStyle layoutStyle)
		{
			LastSelectedLayout = layoutStyle;

			// Map Essentials layout style -> ZRC SDK VideoLayoutStyle int.
			// This must NOT be a direct (int) cast: eLayoutStyle is a [Flags] enum
			// (Gallery=1, Speaker=2, Strip=4, ShareAll=8) whose values do not line up
			// with the SDK's VideoLayoutStyle (Gallery=1, Speaker=2, Thumbnail=3,
			// ContentOnly=4). The previous code cast to SetVideoOrder, which only
			// reorders participant tiles and ignored Strip/ShareAll (out of range).
			int videoLayoutStyle;
			switch (layoutStyle)
			{
				case zConfiguration.eLayoutStyle.Gallery:  videoLayoutStyle = 1; break; // VideoLayoutStyleGallery
				case zConfiguration.eLayoutStyle.Speaker:  videoLayoutStyle = 2; break; // VideoLayoutStyleSpeaker
				case zConfiguration.eLayoutStyle.Strip:    videoLayoutStyle = 3; break; // VideoLayoutStyleThumbnail
				case zConfiguration.eLayoutStyle.ShareAll: videoLayoutStyle = 4; break; // VideoLayoutStyleContentOnly
				default:
					this.LogWarning("SetLayout: no SDK VideoLayoutStyle mapping for {LayoutStyle}", layoutStyle);
					return;
			}

			_controller.UpdateVideoLayoutStyle(videoLayoutStyle);
		}

		public void SwapContentWithThumbnail()
		{
			// Toggle the single-screen floating-share state (content <-> video primary).
			_contentSwappedWithThumbnail = !_contentSwappedWithThumbnail;
			_controller.SwitchToFloatingShareForSingleScreen(_contentSwappedWithThumbnail);
			ContentSwappedWithThumbnailFeedback.FireUpdate();
		}

		public void LayoutTurnNextPage()
		{
			// _currentPageVideoType is kept in sync by the SDK VideoPageStatus notification.
			_controller.TurnVideoPage(true, _currentPageVideoType);
		}

		public void LayoutTurnPreviousPage()
		{
			_controller.TurnVideoPage(false, _currentPageVideoType);
		}

		#endregion

		#region IHasCodecLayouts Members

		private Func<string> LocalLayoutFeedbackFunc
		{
			get
			{
				return () =>
				{
					if (Configuration.Call.Layout.Style != zConfiguration.eLayoutStyle.None)
						return Configuration.Call.Layout.Style.ToString();
					else
						return Configuration.Client.Call.Layout.Style.ToString();
				};
			}
		}

		public StringFeedback LocalLayoutFeedback { get; private set; }

		public void LocalLayoutToggle()
		{
			var currentLayout = LocalLayoutFeedback.StringValue;

			var eCurrentLayout = (int) Enum.Parse(typeof (zConfiguration.eLayoutStyle), currentLayout, true);

			var nextLayout = GetNextLayout(eCurrentLayout);

			if (nextLayout != zConfiguration.eLayoutStyle.None)
			{
				SetLayout(nextLayout);
			}
		}

		/// <summary>
		/// Tries to get the next available layout
		/// </summary>
		/// <param name="currentLayout"></param>
		/// <returns></returns>
		private zConfiguration.eLayoutStyle GetNextLayout(int currentLayout)
		{
			if (AvailableLayouts == zConfiguration.eLayoutStyle.None)
			{
				return zConfiguration.eLayoutStyle.None;
			}

			zConfiguration.eLayoutStyle nextLayout;

			if (((zConfiguration.eLayoutStyle) currentLayout & zConfiguration.eLayoutStyle.ShareAll) ==
			    zConfiguration.eLayoutStyle.ShareAll)
			{
				nextLayout = zConfiguration.eLayoutStyle.Gallery;
			}
			else
			{
				nextLayout = (zConfiguration.eLayoutStyle) (currentLayout << 1);
			}

			if ((AvailableLayouts & nextLayout) == nextLayout)
			{
				return nextLayout;
			}
			else
			{
				return GetNextLayout((int) nextLayout);
			}
		}

		public void LocalLayoutToggleSingleProminent()
		{
			// "Single prominent" == one large active-speaker tile == the SDK's Speaker layout.
			// Toggle between Speaker (prominent) and Gallery using the same VideoLayoutStyle path
			// as SetLayout. LastSelectedLayout is updated by SetLayout so feedback stays in sync.
			var next = LastSelectedLayout == zConfiguration.eLayoutStyle.Speaker
				? zConfiguration.eLayoutStyle.Gallery
				: zConfiguration.eLayoutStyle.Speaker;
			SetLayout(next);
		}

		public void MinMaxLayoutToggle()
		{
			// No direct ZRC SDK equivalent: the SDK exposes discrete VideoLayoutStyle values
			// (Gallery/Speaker/Thumbnail/ContentOnly) but no "minimize/maximize" toggle. The
			// closest single-screen behavior (float share vs. video) is SwapContentWithThumbnail.
			this.LogWarning("MinMaxLayoutToggle has no Zoom Room SDK equivalent; use SwapContentWithThumbnail or a discrete layout instead");
		}

		#endregion

        #region IPasswordPrompt Members

        public event EventHandler<PasswordPromptEventArgs> PasswordRequired;

        public void SubmitPassword(string password)
        {
            _meetingPasswordRequired = false;
            this.LogDebug("Password Submitted: {Password}", password);
            _controller.SendMeetingPassword(password);
        }

        void OnPasswordRequired(bool lastAttemptIncorrect, bool loginFailed, bool loginCancelled, string message)
        {
			_meetingPasswordRequired = !loginFailed || !loginCancelled;

            var handler = PasswordRequired;
            if (handler != null)
            {	            
				this.LogDebug("Meeting Password Required: {MeetingPasswordRequired}", _meetingPasswordRequired);

	            handler(this, new PasswordPromptEventArgs(lastAttemptIncorrect, loginFailed, loginCancelled, message));
            }
        }

        #endregion

        #region IHasMeetingInfo Members

        public event EventHandler<MeetingInfoEventArgs> MeetingInfoChanged;

        private MeetingInfo _meetingInfo;

        public MeetingInfo MeetingInfo
        {
            get { return _meetingInfo; }
            private set
            {
                if (value != _meetingInfo)
                {
                    _meetingInfo = value;

                    var handler = MeetingInfoChanged;
                    if (handler != null)
                    {
                        handler(this, new MeetingInfoEventArgs(_meetingInfo));
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Builds a <see cref="MeetingInfo"/> from the current SDK state and assigns it,
        /// firing <see cref="MeetingInfoChanged"/> if the value has changed.
        /// </summary>
        private void UpdateMeetingInfo()
        {
            MeetingInfo = new MeetingInfo(
                _currentMeetingId,
                _currentMeetingName,
                _sdkHostName,
                string.Empty,
                "None",
                _sdkIsHost,
                _sdkSharingState > 0,
                false,
                _sdkMeetingLocked,
                _sdkIsRecording,
                _sdkCanRecord);
        }

	    #region Implementation of IHasPresentationOnlyMeeting

	    public void StartSharingOnlyMeeting()
	    {
	        StartSharingOnlyMeeting(eSharingMeetingMode.None, 30, String.Empty);
	    }

	    public void StartSharingOnlyMeeting(eSharingMeetingMode displayMode)
	    {
	        StartSharingOnlyMeeting(displayMode, DefaultMeetingDurationMin, String.Empty);
	    }

	    public void StartSharingOnlyMeeting(eSharingMeetingMode displayMode, uint duration)
	    {
	        StartSharingOnlyMeeting(displayMode, duration, String.Empty);
	    }

	    public void StartSharingOnlyMeeting(eSharingMeetingMode displayMode, uint duration, string password)
	    {
            // The SDK launches a sharing-only ("local presentation") meeting with the chosen instruction
            // overlay. duration/password have no equivalent on LaunchSharingMeeting and are not used.
            if (duration != 0 || !string.IsNullOrEmpty(password))
                this.LogDebug("StartSharingOnlyMeeting: duration/password are not supported by the SDK and are ignored");
            _controller.LaunchSharingMeeting(true, SharingModeToSdk(displayMode));
	    }

	    public void StartNormalMeetingFromSharingOnlyMeeting()
	    {
            _controller.SwitchFromLocalPresentationToNormalMeeting();
	    }

	    // Maps Essentials eSharingMeetingMode -> ZRC SDK SharingInstructionDisplayState int.
	    private static int SharingModeToSdk(eSharingMeetingMode mode)
	    {
	        switch (mode)
	        {
	            case eSharingMeetingMode.Laptop: return 1; // SharingInstructionDisplayStateDesktop
	            case eSharingMeetingMode.Ios:    return 2; // SharingInstructionDisplayStateIOS
	            default:                         return 0; // None
	        }
	    }

	    #endregion

        #region IHasMeetingLock Members

        public BoolFeedback MeetingIsLockedFeedback { get; private set; }

        public void LockMeeting()
        {
            _controller.LockMeeting(true);
        }

        public void UnLockMeeting()
        {
            _controller.LockMeeting(false);
        }

        public void ToggleMeetingLock()
        {
            if (MeetingIsLockedFeedback.BoolValue)
            {
                UnLockMeeting();
            }
            else
            {
                LockMeeting();
            }
        }

        #endregion

        #region IHasMeetingRecordingWithPrompt Members

        public BoolFeedback MeetingIsRecordingFeedback { get; private set; }

        bool _recordConsentPromptIsVisible;

        public BoolFeedback RecordConsentPromptIsVisible { get; private set; }

        public void RecordingPromptAcknowledgement(bool agree)
        {
            _controller.ResponseToRecordingRequest(agree);
        }

        public void StartRecording()
        {
            _controller.StartRecording();
        }

        public void StopRecording()
        {
            _controller.StopRecording();
        }

        public void ToggleRecording()
        {
            if (MeetingIsRecordingFeedback.BoolValue)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        #endregion

        #region IZoomWirelessShareInstructions Members

        public event EventHandler<ShareInfoEventArgs> ShareInfoChanged;

        public zStatus.Sharing SharingState
        {
            get
            {
                return Status.Sharing;
            }
        }

        void OnShareInfoChanged(zStatus.Sharing status)
        {
            this.LogDebug(
@"ShareInfoChanged:
isSharingHDMI: {IsSharingHDMI}
isSharingAirplay: {IsSharingAirplay}
AirplayPassword: {AirplayPassword}
OSD Display State: {DispState}
",
status.isSharingBlackMagic,
status.isAirHostClientConnected,
status.password,
status.dispState);

            var handler = ShareInfoChanged;
            if (handler != null)
            {
                handler(this, new ShareInfoEventArgs(status));
            }
        }

        #endregion
    }
}
