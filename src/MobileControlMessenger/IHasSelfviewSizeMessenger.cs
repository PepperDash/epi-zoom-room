using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Core.DeviceTypeInterfaces.IHasSelfviewSize"/>:
    /// set (by command/label) or toggle the selfview PiP size, plus current value and the available options.
    /// </summary>
    public class IHasSelfviewSizeMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasSelfviewSizeMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/toggleSelfviewSize", (id, content) => _codec.SelfviewPipSizeToggle());
            AddAction("/setSelfviewSize", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                var cmd = FindOption(s?.Value);
                if (cmd != null) _codec.SelfviewPipSizeSet(cmd);
            });
        }

        protected override bool CustomActivate()
        {
            _codec.SelfviewPipSizeFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new SelfviewSizeStateMessage { SelfviewPipSize = e.StringValue }));

            return base.CustomActivate();
        }

        private CodecCommandWithLabel FindOption(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return _codec.SelfviewPipSizes.FirstOrDefault(o => string.Equals(o.Command, value, StringComparison.OrdinalIgnoreCase))
                   ?? _codec.SelfviewPipSizes.FirstOrDefault(o => string.Equals(o.Label, value, StringComparison.OrdinalIgnoreCase));
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new SelfviewSizeStateMessage
            {
                SelfviewPipSize = _codec.SelfviewPipSizeFeedback.StringValue,
                AvailableSizes = _codec.SelfviewPipSizes?
                    .Select(o => new SelfviewOption { Command = o.Command, Label = o.Label }).ToList()
            }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasSelfviewSizeMessenger"/>.
    /// </summary>
    public class SelfviewSizeStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("selfviewPipSize", NullValueHandling = NullValueHandling.Ignore)]
        public string SelfviewPipSize { get; set; }

        [JsonProperty("availableSizes", NullValueHandling = NullValueHandling.Ignore)]
        public List<SelfviewOption> AvailableSizes { get; set; }
    }
}
