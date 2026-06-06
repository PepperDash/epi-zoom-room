using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces.IHasMeetingRecordingWithPrompt"/>:
    /// start / stop / toggle recording, acknowledge the consent prompt, plus recording + consent-prompt status.
    /// </summary>
    public class IHasMeetingRecordingWithPromptMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasMeetingRecordingWithPromptMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/startRecording", (id, content) => _codec.StartRecording());
            AddAction("/stopRecording", (id, content) => _codec.StopRecording());
            AddAction("/toggleRecording", (id, content) => _codec.ToggleRecording());
            AddAction("/recordPromptAcknowledge", (id, content) =>
            {
                var b = content?.ToObject<MobileControlSimpleContent<bool>>();
                if (b != null) _codec.RecordingPromptAcknowledgement(b.Value);
            });
        }

        protected override bool CustomActivate()
        {
            _codec.MeetingIsRecordingFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new MeetingRecordingStateMessage { IsRecording = e.BoolValue }));
            _codec.RecordConsentPromptIsVisible.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new MeetingRecordingStateMessage { RecordConsentPromptIsVisible = e.BoolValue }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new MeetingRecordingStateMessage
            {
                IsRecording = _codec.MeetingIsRecordingFeedback.BoolValue,
                RecordConsentPromptIsVisible = _codec.RecordConsentPromptIsVisible.BoolValue
            }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasMeetingRecordingWithPromptMessenger"/>.
    /// </summary>
    public class MeetingRecordingStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("isRecording", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsRecording { get; set; }

        [JsonProperty("recordConsentPromptIsVisible", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RecordConsentPromptIsVisible { get; set; }
    }
}
