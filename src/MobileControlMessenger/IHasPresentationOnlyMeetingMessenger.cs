using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces.IHasPresentationOnlyMeeting"/>:
    /// start a laptop sharing-only meeting and convert it to a normal meeting. Action-only.
    /// </summary>
    public class IHasPresentationOnlyMeetingMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasPresentationOnlyMeetingMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/dialPresent", (id, content) => _codec.StartSharingOnlyMeeting(eSharingMeetingMode.Laptop));
            AddAction("/dialConvert", (id, content) => _codec.StartNormalMeetingFromSharingOnlyMeeting());
        }
    }
}
