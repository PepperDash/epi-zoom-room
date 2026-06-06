using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces.IHasParticipantPinUnpin"/>:
    /// pin / unpin / toggle a participant to a screen, plus the available screen count.
    /// </summary>
    public class IHasParticipantPinUnpinMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasParticipantPinUnpinMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/pinParticipant", (id, content) =>
            {
                var c = content?.ToObject<ParticipantPinContent>();
                if (c != null) _codec.PinParticipant(c.UserId, c.ScreenIndex);
            });
            AddAction("/unpinParticipant", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.UnPinParticipant(i.Value);
            });
            AddAction("/toggleParticipantPin", (id, content) =>
            {
                var c = content?.ToObject<ParticipantPinContent>();
                if (c != null) _codec.ToggleParticipantPinState(c.UserId, c.ScreenIndex);
            });
        }

        protected override bool CustomActivate()
        {
            _codec.NumberOfScreensFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new ParticipantPinStateMessage { NumberOfScreens = e.IntValue }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(
                new ParticipantPinStateMessage { NumberOfScreens = _codec.NumberOfScreensFeedback.IntValue }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasParticipantPinUnpinMessenger"/>.
    /// </summary>
    public class ParticipantPinStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("numberOfScreens", NullValueHandling = NullValueHandling.Ignore)]
        public int? NumberOfScreens { get; set; }
    }
}
