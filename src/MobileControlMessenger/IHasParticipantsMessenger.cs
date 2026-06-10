using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="IHasParticipants"/>: roster status plus remove / set-host /
    /// admit-from-waiting-room actions.
    /// </summary>
    public class IHasParticipantsMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasParticipantsMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/removeParticipant", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.RemoveParticipant(i.Value);
            });
            AddAction("/setParticipantAsHost", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.SetParticipantAsHost(i.Value);
            });
            AddAction("/admitParticipantFromWaitingRoom", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.AdmitParticipantFromWaitingRoom(i.Value);
            });
            AddAction("/admitAllFromWaitingRoom", (id, content) => _codec.AdmitAllParticipantsFromWaitingRoom());
            // Remove from the waiting room = expel/deny that user (same path the codec uses for removeParticipant).
            AddAction("/removeFromWaitingRoom", (id, content) =>
            {
                var i = content?.ToObject<MobileControlSimpleContent<int>>();
                if (i != null) _codec.RemoveParticipant(i.Value);
            });
            AddAction("/removeAllFromWaitingRoom", (id, content) => _codec.RemoveAllFromWaitingRoom());
        }

        protected override bool CustomActivate()
        {
            _codec.Participants.ParticipantsListHasChanged += (s, e) =>
                Task.Run(() => PostStatusMessage(BuildStatus()));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) => Task.Run(() => PostStatusMessage(BuildStatus(), id));

        private ParticipantsStateMessage BuildStatus() =>
            new ParticipantsStateMessage
            {
                Participants = _codec.GetParticipantsSnapshot(),
                WaitingRoom = _codec.WaitingRoomParticipants
            };
    }

    /// <summary>
    /// Status payload for <see cref="IHasParticipantsMessenger"/>.
    /// </summary>
    public class ParticipantsStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("participants", NullValueHandling = NullValueHandling.Ignore)]
        public List<Participant> Participants { get; set; }

        [JsonProperty("waitingRoom", NullValueHandling = NullValueHandling.Ignore)]
        public List<Participant> WaitingRoom { get; set; }
    }
}
