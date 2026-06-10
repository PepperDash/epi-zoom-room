using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Core.DeviceTypeInterfaces.IHasPhoneDialing"/>:
    /// dial / hang up / send DTMF, plus off-hook and caller-ID status.
    /// </summary>
    public class IHasPhoneDialingMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasPhoneDialingMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/dialPhoneCall", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                if (!string.IsNullOrEmpty(s?.Value)) _codec.DialPhoneCall(s.Value);
            });
            AddAction("/endPhoneCall", (id, content) => _codec.EndPhoneCall());
            AddAction("/sendDtmfToPhone", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                if (!string.IsNullOrEmpty(s?.Value)) _codec.SendDtmfToPhone(s.Value);
            });
        }

        protected override bool CustomActivate()
        {
            _codec.PhoneOffHookFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new PhoneDialingStateMessage { PhoneOffHook = e.BoolValue }));
            _codec.CallerIdNameFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new PhoneDialingStateMessage { CallerIdName = e.StringValue }));
            _codec.CallerIdNumberFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new PhoneDialingStateMessage { CallerIdNumber = e.StringValue }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new PhoneDialingStateMessage
            {
                PhoneOffHook = _codec.PhoneOffHookFeedback.BoolValue,
                CallerIdName = _codec.CallerIdNameFeedback.StringValue,
                CallerIdNumber = _codec.CallerIdNumberFeedback.StringValue
            }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasPhoneDialingMessenger"/>.
    /// </summary>
    public class PhoneDialingStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("phoneOffHook", NullValueHandling = NullValueHandling.Ignore)]
        public bool? PhoneOffHook { get; set; }

        [JsonProperty("callerIdName", NullValueHandling = NullValueHandling.Ignore)]
        public string CallerIdName { get; set; }

        [JsonProperty("callerIdNumber", NullValueHandling = NullValueHandling.Ignore)]
        public string CallerIdNumber { get; set; }
    }
}
