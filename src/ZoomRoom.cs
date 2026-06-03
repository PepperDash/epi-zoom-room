using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private bool _sdkIsRecording;
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

        private IHasCameraControls _selectedCamera;
        private CodecDirectory _currentDirectoryResult;

        private readonly ZoomRoomPropertiesConfig _props;

        public ZoomRoom(DeviceConfig config, IZoomRoomController controller)
			: base(config)
		{
            DefaultMeetingDurationMin = 30;

			_props = JsonConvert.DeserializeObject<ZoomRoomPropertiesConfig>(config.Properties.ToString());

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
			get { return () => 0; } // Volume not supported by Zoom Room SDK
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

		protected Func<bool> FarEndIsSharingContentFeedbackFunc
		{
			get { return () => false; } // SDK does not report far-end sharing state separately
		}

		protected override Func<bool> MuteFeedbackFunc
		{
			get { return () => false; } // Volume mute not supported by Zoom Room SDK
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
			get { return () => false; } // Self-view not supported by Zoom Room SDK
		}

		protected Func<bool> CameraIsOffFeedbackFunc
		{
			get { return () => _sdkCameraOff; }
		}

		protected Func<bool> CameraAutoModeIsOnFeedbackFunc
		{
			get { return () => false; }
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
            this.LogWarning("SelectCamera not supported by Zoom Room SDK");
        }

		public CameraBase FarEndCamera { get; private set; }

		public BoolFeedback ControllingFarEndCameraFeedback { get; private set; }

		#endregion

		#region IHasCodecSelfView Members

		public BoolFeedback SelfviewIsOnFeedback { get; private set; }

		public void GetSelfViewMode() { this.LogWarning("SelfView not supported by Zoom Room SDK"); }

		public void SelfViewModeOn() { this.LogWarning("SelfView not supported by Zoom Room SDK"); }

		public void SelfViewModeOff() { this.LogWarning("SelfView not supported by Zoom Room SDK"); }

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
			this.LogWarning("Schedule/bookings not supported by Zoom Room SDK");
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
            _controller.RecordingRequestReceived += OnControllerRecordingRequestReceived;
            _controller.ParticipantsInitialized  += OnControllerParticipantsInitialized;
            _controller.UserJoined               += OnControllerUserJoined;
            _controller.UserLeft                 += OnControllerUserLeft;
            _controller.UserUpdated              += OnControllerUserUpdated;
            _controller.ParticipantCountChanged  += (s, e) => Participants.OnParticipantsChanged();
            _controller.HostChanged              += OnControllerHostChanged;
            _controller.SharingStatusChanged     += OnControllerSharingStatusChanged;
            _controller.SipCallStatusChanged     += OnControllerSipCallStatusChanged;

            _controller.Initialize(_props.SdkConfigPath);
	    }

	    #endregion

        // ── SDK event handlers ───────────────────────────────────────────────

        private void OnControllerConnectionStateChanged(object sender, SdkEventArgs e)
        {
            var connected = e.ErrorCode == (int)ConnectionState.Established || e.ErrorCode == (int)ConnectionState.Connected;
            this.LogInformation("SDK connection state changed: {State} ({Code})", (ConnectionState)e.ErrorCode, e.ErrorCode);

            ((SdkConnectionMonitor)CommunicationMonitor).SetOnline(connected);

            if (!connected)
            {
                // Reset in-call state on disconnect
                ActiveCalls.Clear();
                Participants.CurrentParticipants = new System.Collections.Generic.List<Participant>();
                OnCallStatusChange(new CodecActiveCallItem { Status = eCodecCallStatus.Disconnected });
            }
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

        private void OnControllerRecordingRequestReceived(object sender, SdkEventArgs e)
        {
            _recordConsentPromptIsVisible = true;
            RecordConsentPromptIsVisible.FireUpdate();
        }

        private void OnControllerParticipantsInitialized(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            Participants.CurrentParticipants = MapParticipants(e.Participants);
        }

        private void OnControllerUserJoined(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            foreach (var info in e.Participants)
            {
                var existing = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId == info.UserID);
                if (existing == null)
                {
                    var p = MapParticipant(info);
                    Participants.CurrentParticipants.Add(p);
                }
            }
            Participants.OnParticipantsChanged();
        }

        private void OnControllerUserLeft(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            foreach (var info in e.Participants)
            {
                var existing = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId == info.UserID);
                if (existing != null)
                    Participants.CurrentParticipants.Remove(existing);
            }
            Participants.OnParticipantsChanged();
        }

        private void OnControllerUserUpdated(object sender, ParticipantListEventArgs e)
        {
            if (e?.Participants == null) return;
            foreach (var info in e.Participants)
            {
                var existing = Participants.CurrentParticipants.FirstOrDefault(p => p.UserId == info.UserID);
                if (existing != null)
                {
                    existing.Name       = info.UserName;
                    existing.IsHost     = info.IsHost;
                    existing.AudioMuteFb = info.AudioMuted;
                }
            }
            Participants.OnParticipantsChanged();
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
                UserId       = info.UserID,
                Name         = info.UserName,
                IsHost       = info.IsHost,
                AudioMuteFb  = info.AudioMuted,
            };
        }


        protected override void OnCallStatusChange(CodecActiveCallItem item)
        {
            base.OnCallStatusChange(item);
        }

        /// <summary>
        /// Starts sharing HDMI source
        /// </summary>
		public override void StartSharing() { this.LogWarning("StartSharing not supported by Zoom Room SDK"); }

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

		public override void MuteOff()
		{
			this.LogWarning("Volume mute not supported by Zoom Room SDK");
		}

		public override void MuteOn()
		{
			SetVolume(0);
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
			// TODO: Implment volume decrement that calls SetVolume()
		}

		/// <summary>
		/// Decrements the volume
		/// </summary>
		/// <param name="pressRelease"></param>
		public override void VolumeDown(bool pressRelease)
		{
			// TODO: Implment volume decrement that calls SetVolume()
		}

		/// <summary>
		/// Scales the level and sets the codec to the specified level within its range
		/// </summary>
		/// <param name="level">level from slider (0-65535 range)</param>
		public override void SetVolume(ushort level)
		{
			this.LogWarning("Volume control not supported by Zoom Room SDK");
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
			this.LogWarning("RejectCall not supported by Zoom Room SDK");
			if (call != null)
			{
				call.Status = eCodecCallStatus.Disconnected;
				OnCallStatusChange(call);
			}
		}

		public override void Dial(Meeting meeting)
		{
			this.LogInformation("Dialing meeting.Id: {MeetingId} Title: {MeetingTitle}", meeting.Id, meeting.Title);
			_controller.JoinMeeting(meeting.Id);
		}

		public override void Dial(string number)
		{
			this.LogDebug("Dialing number: {Number}", number);
			_controller.JoinMeeting(number);
		}

        /// <summary>
        /// Dials a meeting with a password
        /// </summary>
        public void Dial(string number, string password)
        {
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
            var ic = contact as InvitableDirectoryContact;

			if (ic != null)
			{
				this.LogWarning("Dial(IInvitableContact) not supported by Zoom Room SDK");
			}
		}

        /// <summary>
        /// Invites contacts to a new meeting for a specified duration
        /// </summary>
        /// <param name="contacts"></param>
        /// <param name="duration"></param>
        public void InviteContactsToNewMeeting(List<InvitableDirectoryContact> contacts, uint duration)
        {
            this.LogWarning("InviteContactsToNewMeeting not supported by Zoom Room SDK");
        }

        /// <summary>
        /// Invites contacts to an existing meeting
        /// </summary>
        /// <param name="contacts"></param>
        public void InviteContactsToExistingMeeting(List<InvitableDirectoryContact> contacts)
        {
            this.LogWarning("InviteContactsToExistingMeeting not supported by Zoom Room SDK");
        }


        /// <summary>
        /// Starts a PMI Meeting for the specified duration (or default meeting duration if 0 is specified)
        /// </summary>
        /// <param name="duration">duration of meeting</param>
        public void StartMeeting(uint duration)
        {
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
			SelectedCameraFeedback = new StringFeedback(() => Configuration.Video.Camera.SelectedId);

			ControllingFarEndCameraFeedback = new BoolFeedback(() => SelectedCamera is IAmFarEndCamera);

			foreach (var cam in Status.Cameras)
			{
				// Known Issue:
				// Crestron UC engine systems seem to report an item in the cameras list that represnts the USB bridge device. 
				// If we know the name and it's reliably consistent, we could ignore it here...

				if (cam.Name.IndexOf("HD-CONV-USB") > -1)
				{
					// Skip this as it's the Crestron USB box, not a real camera
					continue;
				}

                var existingCam = Cameras.FirstOrDefault((c) => c.Key.Equals(cam.id));

                if (existingCam == null)
                {
                    var camera = new ZoomRoomCamera(cam.id, cam.Name, this);

                    Cameras.Add(camera);

                    if (cam.Selected)
                    {
                        SelectedCamera = camera;
                    }
                }
			}

			if (IsInCall)
			{
				UpdateFarEndCameras();
			}
		}

		/// <summary>
		/// Dynamically creates far end cameras for call participants who have far end control enabled.
		/// </summary>
		private void UpdateFarEndCameras()
		{
			// TODO: set up far end cameras for the current call
		}

		#region Implementation of IHasParticipants

		public CodecParticipants Participants { get; private set; }

        public void RemoveParticipant(int userId)
        {
            this.LogWarning("RemoveParticipant not supported by Zoom Room SDK");
        }

        public void SetParticipantAsHost(int userId)
        {
            this.LogWarning("SetParticipantAsHost not supported by Zoom Room SDK");
        }

        public void AdmitParticipantFromWaitingRoom(int userId)
        {
            _controller.AdmitUserFromWaitingRoom(userId);
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
			this.LogWarning("MuteVideoForParticipant not supported by Zoom Room SDK");
		}

		public void UnmuteVideoForParticipant(int userId)
		{
			this.LogWarning("UnmuteVideoForParticipant not supported by Zoom Room SDK");
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

		public void CameraAutoModeOn()
		{
			this.LogWarning("CameraAutoModeOn not supported by Zoom Room SDK");
		}

		public void CameraAutoModeOff()
		{
			this.LogWarning("CameraAutoModeOff not supported by Zoom Room SDK");
		}

		public void CameraAutoModeToggle()
		{
			this.LogWarning("CameraAutoModeToggle not supported by Zoom Room SDK");
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
			this.LogWarning("SelfviewPipPositionSet not supported by Zoom Room SDK");
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

		public StringFeedback SelfviewPipSizeFeedback { get; private set; }

		public void SelfviewPipSizeSet(CodecCommandWithLabel size)
		{
			this.LogWarning("SelfviewPipSizeSet not supported by Zoom Room SDK");
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
			this.LogWarning("DialPhoneCall not supported by Zoom Room SDK");
		}

		public void EndPhoneCall()
		{
			_controller.TerminateSipCall(_activeSipCallId);
		}

		public void SendDtmfToPhone(string digit)
		{
			this.LogWarning("SendDtmfToPhone not supported by Zoom Room SDK");
		}

		#endregion

		#region IHasZoomRoomLayouts Members

		public event EventHandler<LayoutInfoChangedEventArgs> LayoutInfoChanged;

		private Func<bool> LayoutViewIsOnFirstPageFeedbackFunc
		{
			get { return () => Status.Layout.is_In_First_Page; }
		}

		private Func<bool> LayoutViewIsOnLastPageFeedbackFunc
		{
			get { return () => Status.Layout.is_In_Last_Page; }
		}

		private Func<bool> CanSwapContentWithThumbnailFeedbackFunc
		{
			get { return () => Status.Layout.can_Switch_Floating_Share_Content; }
		}

		private Func<bool> ContentSwappedWithThumbnailFeedbackFunc
		{
			get { return () => Configuration.Call.Layout.ShareThumb; }
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
			_controller.SetVideoOrder((int)layoutStyle);
		}

		public void SwapContentWithThumbnail()
		{
			this.LogWarning("SwapContentWithThumbnail not supported by Zoom Room SDK");
		}

		public void LayoutTurnNextPage()
		{
			this.LogWarning("LayoutTurnNextPage not supported by Zoom Room SDK");
		}

		public void LayoutTurnPreviousPage()
		{
			this.LogWarning("LayoutTurnPreviousPage not supported by Zoom Room SDK");
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
			this.LogWarning("LocalLayoutToggleSingleProminent not supported by Zoom Room SDK");
		}

		public void MinMaxLayoutToggle()
		{
			this.LogWarning("MinMaxLayoutToggle not supported by Zoom Room SDK");
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
                false);
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
            this.LogWarning("StartSharingOnlyMeeting not supported by Zoom Room SDK");
	    }

	    public void StartNormalMeetingFromSharingOnlyMeeting()
	    {
            this.LogWarning("StartNormalMeetingFromSharingOnlyMeeting not supported by Zoom Room SDK");
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
