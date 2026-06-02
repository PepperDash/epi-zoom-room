using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using PepperDash.Essentials.Plugins.Zoom.Room;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ZoomRoomMessenger : MessengerBase
    {
        private readonly ZoomRoom _codec;

        public ZoomRoomMessenger(string key, string messagePath, EssentialsDevice device)
            : base(key, messagePath, device)
        {
            _codec = device as ZoomRoom ?? throw new ArgumentNullException(nameof(device));
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/startMeeting", (id, content) =>
            {
                var msg = content?.ToObject<MobileControlSimpleContent<uint>>();
                _codec.StartMeeting(msg?.Value ?? _codec.DefaultMeetingDurationMin);
            });

            AddAction("/leaveMeeting", (id, content) => _codec.LeaveMeeting());

            AddAction("/invite", (id, content) =>
            {
                var c = content?.ToObject<InvitableDirectoryContact>();
                if (c != null) _codec.Dial(c);
            });

            AddAction("/inviteContactsToNewMeeting", (id, content) =>
            {
                var c = content?.ToObject<Invitation>();
                if (c != null) _codec.InviteContactsToNewMeeting(c.Invitees, c.Duration);
            });

            AddAction("/inviteContactsToExistingMeeting", (id, content) =>
            {
                var c = content?.ToObject<Invitation>();
                if (c != null) _codec.InviteContactsToExistingMeeting(c.Invitees);
            });

            AddAction("/muteVideo", (id, content) => _codec.CameraMuteOn());
            AddAction("/toggleVideoMute", (id, content) => _codec.CameraMuteToggle());

            AddAction("/endMeeting", (id, content) => _codec.EndAllCalls());

            var presentOnlyMeetingCodec = _codec as IHasPresentationOnlyMeeting;
            if (presentOnlyMeetingCodec != null)
            {
                AddAction("/dialPresent", (id, content) =>
                    presentOnlyMeetingCodec.StartSharingOnlyMeeting(eSharingMeetingMode.Laptop));
                AddAction("/dialConvert", (id, content) =>
                    presentOnlyMeetingCodec.StartNormalMeetingFromSharingOnlyMeeting());
            }

            var participantsCodec = _codec as IHasParticipants;
            if (participantsCodec != null)
            {
                AddAction("/removeParticipant", (id, content) =>
                {
                    var i = content?.ToObject<MobileControlSimpleContent<int>>();
                    if (i != null) participantsCodec.RemoveParticipant(i.Value);
                });
                AddAction("/setParticipantAsHost", (id, content) =>
                {
                    var i = content?.ToObject<MobileControlSimpleContent<int>>();
                    if (i != null) participantsCodec.SetParticipantAsHost(i.Value);
                });

                var audioMuteCodec = _codec as IHasParticipantAudioMute;
                if (audioMuteCodec != null)
                {
                    AddAction("/muteAllParticipants", (id, content) =>
                        audioMuteCodec.MuteAudioForAllParticipants());
                    AddAction("/toggleParticipantAudioMute", (id, content) =>
                    {
                        var i = content?.ToObject<MobileControlSimpleContent<int>>();
                        if (i != null) audioMuteCodec.ToggleAudioForParticipant(i.Value);
                    });
                    AddAction("/toggleParticipantVideoMute", (id, content) =>
                    {
                        var i = content?.ToObject<MobileControlSimpleContent<int>>();
                        if (i != null) audioMuteCodec.ToggleVideoForParticipant(i.Value);
                    });
                }
            }

            var lockCodec = _codec as IHasMeetingLock;
            if (lockCodec != null)
            {
                AddAction("/toggleMeetingLock", (id, content) => lockCodec.ToggleMeetingLock());
            }

            var recordCodec = _codec as IHasMeetingRecordingWithPrompt;
            if (recordCodec != null)
            {
                AddAction("/recordPromptAcknowledge", (id, content) =>
                {
                    var b = content?.ToObject<MobileControlSimpleContent<bool>>();
                    if (b != null) recordCodec.RecordingPromptAcknowledgement(b.Value);
                });
                AddAction("/toggleRecording", (id, content) => recordCodec.ToggleRecording());
            }

            var layoutsCodec = _codec as IHasZoomRoomLayouts;
            if (layoutsCodec != null)
            {
                AddAction("/selectLayout", (id, content) =>
                {
                    var s = content?.ToObject<MobileControlSimpleContent<string>>();
                    if (s != null)
                        layoutsCodec.SetLayout((zConfiguration.eLayoutStyle)Enum.Parse(
                            typeof(zConfiguration.eLayoutStyle), s.Value, true));
                });
                AddAction("/participantsNextPage", (id, content) => layoutsCodec.LayoutTurnNextPage());
                AddAction("/participantsPreviousPage", (id, content) => layoutsCodec.LayoutTurnPreviousPage());
            }
        }

        protected override bool CustomActivate()
        {
            _codec.CameraIsMutedFeedback.OutputChange += CameraIsMutedFeedback_OutputChange;
            _codec.VideoUnmuteRequested += Codec_VideoUnmuteRequested;

            var participantsCodec = _codec as IHasParticipants;
            if (participantsCodec != null)
                participantsCodec.Participants.ParticipantsListHasChanged += Participants_ParticipantsListHasChanged;

            var lockCodec = _codec as IHasMeetingLock;
            if (lockCodec != null)
                lockCodec.MeetingIsLockedFeedback.OutputChange += MeetingIsLockedFeedback_OutputChange;

            var recordCodec = _codec as IHasMeetingRecordingWithPrompt;
            if (recordCodec != null)
            {
                recordCodec.MeetingIsRecordingFeedback.OutputChange += MeetingIsRecordingFeedback_OutputChange;
                recordCodec.RecordConsentPromptIsVisible.OutputChange += RecordConsentPromptIsVisible_OutputChange;
            }

            var layoutsCodec = _codec as IHasZoomRoomLayouts;
            if (layoutsCodec != null)
                layoutsCodec.LayoutInfoChanged += LayoutsCodec_LayoutInfoChanged;

            var meetingInfoCodec = _codec as IHasMeetingInfo;
            if (meetingInfoCodec != null)
                meetingInfoCodec.MeetingInfoChanged += MeetingInfoCodec_MeetingInfoChanged;

            var scheduleCodec = _codec as IHasScheduleAwareness;
            if (scheduleCodec != null)
                scheduleCodec.CodecSchedule.MeetingsListHasChanged += CodecSchedule_MeetingsListHasChanged;

            var sharingInfo = _codec as IZoomWirelessShareInstructions;
            if (sharingInfo != null)
                sharingInfo.ShareInfoChanged += SharingInfo_ShareInfoChanged;

            return base.CustomActivate();
        }

        private void SendFullStatus(string clientId = null)
        {
            if (!_codec.IsReady) return;

            try
            {
                var status = new ZoomRoomStateMessage
                {
                    CurrentDirectory = _codec.DirectoryRoot,
                    Meetings = _codec.CodecSchedule.Meetings,
                    Participants = _codec.Participants.CurrentParticipants,
                    CameraIsMuted = _codec.CameraIsMutedFeedback.BoolValue,
                    RecordConsentPromptIsVisible = _codec.RecordConsentPromptIsVisible.BoolValue,
                    ShareInfo = _codec.SharingState,
                    Layouts = BuildLayoutState()
                };

                Task.Run(() => PostStatusMessage(status, clientId));
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error sending ZoomRoom full status");
            }
        }

        private ZoomRoomLayoutState BuildLayoutState()
        {
            var layoutsCodec = _codec as IHasZoomRoomLayouts;
            if (layoutsCodec == null) return null;

            return new ZoomRoomLayoutState
            {
                AvailableLayouts = layoutsCodec.AvailableLayouts,
                LayoutViewIsOnFirstPage = layoutsCodec.LayoutViewIsOnFirstPageFeedback.BoolValue,
                LayoutViewIsOnLastPage = layoutsCodec.LayoutViewIsOnLastPageFeedback.BoolValue,
                CanSwapContentWithThumbnail = _codec.CanSwapContentWithThumbnailFeedback.BoolValue,
                ContentSwappedWithThumbnail = _codec.ContentSwappedWithThumbnailFeedback.BoolValue
            };
        }

        private void SharingInfo_ShareInfoChanged(object sender, ShareInfoEventArgs e)
        {
            var status = new ZoomRoomStateMessage { ShareInfo = e.SharingStatus };
            Task.Run(() => PostStatusMessage(status));
        }

        private void CodecSchedule_MeetingsListHasChanged(object sender, EventArgs e)
        {
            var status = new ZoomRoomStateMessage
            {
                Meetings = (_codec as IHasScheduleAwareness)?.CodecSchedule.Meetings
            };
            Task.Run(() => PostStatusMessage(status));
        }

        private void RecordConsentPromptIsVisible_OutputChange(object sender, FeedbackEventArgs e)
        {
            var status = new ZoomRoomStateMessage { RecordConsentPromptIsVisible = e.BoolValue };
            Task.Run(() => PostStatusMessage(status));
        }

        private void CameraIsMutedFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var status = new ZoomRoomStateMessage { CameraIsMuted = e.BoolValue };
            Task.Run(() => PostStatusMessage(status));
        }

        private void MeetingInfoCodec_MeetingInfoChanged(object sender, MeetingInfoEventArgs e)
        {
            var status = new ZoomRoomStateMessage { MeetingInfo = e.Info };
            Task.Run(() => PostStatusMessage(status));
        }

        private void Codec_VideoUnmuteRequested(object sender, EventArgs e)
        {
            var eventMsg = new ZoomRoomEventMessage { EventType = "videoUnmuteRequested" };
            Task.Run(() => PostEventMessage(eventMsg));
        }

        private void Participants_ParticipantsListHasChanged(object sender, EventArgs e)
        {
            var status = new ZoomRoomStateMessage { Participants = _codec.Participants.CurrentParticipants };
            Task.Run(() => PostStatusMessage(status));
        }

        private void MeetingIsRecordingFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var status = new ZoomRoomStateMessage { MeetingInfo = _codec.MeetingInfo };
            Task.Run(() => PostStatusMessage(status));
        }

        private void MeetingIsLockedFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var status = new ZoomRoomStateMessage { MeetingInfo = _codec.MeetingInfo };
            Task.Run(() => PostStatusMessage(status));
        }

        private void LayoutsCodec_LayoutInfoChanged(object sender, LayoutInfoChangedEventArgs e)
        {
            var status = new ZoomRoomStateMessage
            {
                Layouts = new ZoomRoomLayoutState
                {
                    AvailableLayouts = e.AvailableLayouts,
                    LayoutViewIsOnFirstPage = e.LayoutViewIsOnFirstPage
                }
            };
            Task.Run(() => PostStatusMessage(status));
        }
    }

    public class Invitation
    {
        [JsonProperty("duration")]
        public uint Duration { get; set; }
        [JsonProperty("invitees")]
        public List<InvitableDirectoryContact> Invitees { get; set; }
    }

    public class ZoomRoomStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("layouts", NullValueHandling = NullValueHandling.Ignore)]
        public ZoomRoomLayoutState Layouts { get; set; }

        [JsonProperty("meetings", NullValueHandling = NullValueHandling.Ignore)]
        public List<Meeting> Meetings { get; set; }

        [JsonProperty("participants", NullValueHandling = NullValueHandling.Ignore)]
        public List<Participant> Participants { get; set; }

        [JsonProperty("cameraIsMuted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraIsMuted { get; set; }

        [JsonProperty("recordConsentPromptIsVisible", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RecordConsentPromptIsVisible { get; set; }

        [JsonProperty("shareInfo", NullValueHandling = NullValueHandling.Ignore)]
        public zStatus.Sharing ShareInfo { get; set; }

        [JsonProperty("currentDirectory", NullValueHandling = NullValueHandling.Ignore)]
        public CodecDirectory CurrentDirectory { get; set; }

        [JsonProperty("meetingInfo", NullValueHandling = NullValueHandling.Ignore)]
        public MeetingInfo MeetingInfo { get; set; }
    }

    public class ZoomRoomLayoutState
    {
        [JsonProperty("availableLayouts", NullValueHandling = NullValueHandling.Ignore)]
        public zConfiguration.eLayoutStyle AvailableLayouts { get; set; }

        [JsonProperty("layoutViewIsOnFirstPage", NullValueHandling = NullValueHandling.Ignore)]
        public bool LayoutViewIsOnFirstPage { get; set; }

        [JsonProperty("layoutViewIsOnLastPage", NullValueHandling = NullValueHandling.Ignore)]
        public bool LayoutViewIsOnLastPage { get; set; }

        [JsonProperty("canSwapContentWithThumbnail", NullValueHandling = NullValueHandling.Ignore)]
        public bool CanSwapContentWithThumbnail { get; set; }

        [JsonProperty("contentSwappedWithThumbnail", NullValueHandling = NullValueHandling.Ignore)]
        public bool ContentSwappedWithThumbnail { get; set; }
    }

    public class ZoomRoomEventMessage : DeviceEventMessageBase
    {
    }
}

