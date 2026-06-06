using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core.Logging;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.Devices.Common.Codec;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Device-level Mobile Control messenger for the Zoom Room: the glue actions/status that don't map to a
    /// single reusable capability interface (dialing/inviting, near-end camera mute + unmute-request event,
    /// ending the meeting, and the directory root). Capability-specific behavior lives in the dedicated
    /// I{Interface}Messenger classes registered alongside this one.
    /// </summary>
    public class ZoomRoomMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public ZoomRoomMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            // NOTE: /startMeeting and /leaveMeeting are intentionally NOT registered — the core
            // IHasStartMeetingMessenger (auto-registered for IHasStartMeeting) handles them at this same
            // /device/{key} path. Registering them here too would double-fire (the controller dispatches a
            // path to every messenger registered under it).

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

            // Join a scheduled meeting by its id. Resolves the Meeting from the codec schedule so the
            // device logs the title; falls back to dialing the id directly if it's not on the schedule.
            AddAction("/joinScheduledMeeting", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                if (string.IsNullOrEmpty(s?.Value)) return;
                var meeting = _codec.CodecSchedule?.Meetings?.FirstOrDefault(m => m.Id == s.Value);
                if (meeting != null) _codec.Dial(meeting);
                else _codec.Dial(s.Value);
            });

            // Join a meeting by number, optionally with a passcode. With no passcode the codec will raise
            // the IPasswordPrompt flow if the meeting is locked (see IPasswordPromptMessenger).
            AddAction("/joinMeeting", (id, content) =>
            {
                var c = content?.ToObject<JoinMeetingContent>();
                if (string.IsNullOrEmpty(c?.MeetingNumber)) return;
                if (string.IsNullOrEmpty(c.Password)) _codec.Dial(c.MeetingNumber);
                else _codec.Dial(c.MeetingNumber, c.Password);
            });
        }

        protected override bool CustomActivate()
        {
            _codec.CameraIsMutedFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new ZoomRoomStateMessage { CameraIsMuted = e.BoolValue }));

            _codec.VideoUnmuteRequested += (s, e) =>
                Task.Run(() => PostEventMessage(new ZoomRoomEventMessage { EventType = "videoUnmuteRequested" }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string clientId = null)
        {
            if (!_codec.IsReady) return;

            try
            {
                var status = new ZoomRoomStateMessage
                {
                    CameraIsMuted = _codec.CameraIsMutedFeedback.BoolValue,
                    CurrentDirectory = _codec.DirectoryRoot
                };

                Task.Run(() => PostStatusMessage(status, clientId));
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error sending ZoomRoom full status");
            }
        }
    }

    /// <summary>
    /// Device-level status payload: near-end camera mute and the directory root.
    /// </summary>
    public class ZoomRoomStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("cameraIsMuted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraIsMuted { get; set; }

        [JsonProperty("currentDirectory", NullValueHandling = NullValueHandling.Ignore)]
        public CodecDirectory CurrentDirectory { get; set; }
    }

    /// <summary>
    /// One-shot event payload for the Zoom Room (e.g. "videoUnmuteRequested").
    /// </summary>
    public class ZoomRoomEventMessage : DeviceEventMessageBase
    {
    }
}
