using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces.IHasParticipantAudioMute"/>:
    /// mute-all and per-participant audio/video mute toggles. Action-only (no status of its own).
    /// </summary>
    public class IHasParticipantAudioMuteMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasParticipantAudioMuteMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/muteAllParticipants", (id, content) => _codec.MuteAudioForAllParticipants());
            AddAction("/toggleParticipantAudioMute", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.ToggleAudioForParticipant(i.Value);
            });
            AddAction("/toggleParticipantVideoMute", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.ToggleVideoForParticipant(i.Value);
            });
        }
    }
}
