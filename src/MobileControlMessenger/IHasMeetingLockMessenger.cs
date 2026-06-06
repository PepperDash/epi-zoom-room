using System.Threading.Tasks;
using Newtonsoft.Json;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces.IHasMeetingLock"/>:
    /// lock / unlock / toggle the meeting, plus locked status.
    /// </summary>
    public class IHasMeetingLockMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasMeetingLockMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/lockMeeting", (id, content) => _codec.LockMeeting());
            AddAction("/unlockMeeting", (id, content) => _codec.UnLockMeeting());
            AddAction("/toggleMeetingLock", (id, content) => _codec.ToggleMeetingLock());
        }

        protected override bool CustomActivate()
        {
            _codec.MeetingIsLockedFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new MeetingLockStateMessage { MeetingIsLocked = e.BoolValue }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(
                new MeetingLockStateMessage { MeetingIsLocked = _codec.MeetingIsLockedFeedback.BoolValue }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasMeetingLockMessenger"/>.
    /// </summary>
    public class MeetingLockStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("meetingIsLocked", NullValueHandling = NullValueHandling.Ignore)]
        public bool? MeetingIsLocked { get; set; }
    }
}
