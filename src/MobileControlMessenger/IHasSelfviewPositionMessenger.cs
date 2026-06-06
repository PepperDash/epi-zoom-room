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
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Core.DeviceTypeInterfaces.IHasSelfviewPosition"/>:
    /// set (by command/label) or toggle the selfview PiP position, plus current value and the available options.
    /// </summary>
    public class IHasSelfviewPositionMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasSelfviewPositionMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/toggleSelfviewPosition", (id, content) => _codec.SelfviewPipPositionToggle());
            AddAction("/setSelfviewPosition", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                var cmd = FindOption(s?.Value);
                if (cmd != null) _codec.SelfviewPipPositionSet(cmd);
            });
        }

        protected override bool CustomActivate()
        {
            _codec.SelfviewPipPositionFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new SelfviewPositionStateMessage { SelfviewPipPosition = e.StringValue }));

            return base.CustomActivate();
        }

        private CodecCommandWithLabel FindOption(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return _codec.SelfviewPipPositions.FirstOrDefault(o => string.Equals(o.Command, value, StringComparison.OrdinalIgnoreCase))
                   ?? _codec.SelfviewPipPositions.FirstOrDefault(o => string.Equals(o.Label, value, StringComparison.OrdinalIgnoreCase));
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new SelfviewPositionStateMessage
            {
                SelfviewPipPosition = _codec.SelfviewPipPositionFeedback.StringValue,
                AvailablePositions = _codec.SelfviewPipPositions?
                    .Select(o => new SelfviewOption { Command = o.Command, Label = o.Label }).ToList()
            }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasSelfviewPositionMessenger"/>.
    /// </summary>
    public class SelfviewPositionStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("selfviewPipPosition", NullValueHandling = NullValueHandling.Ignore)]
        public string SelfviewPipPosition { get; set; }

        [JsonProperty("availablePositions", NullValueHandling = NullValueHandling.Ignore)]
        public List<SelfviewOption> AvailablePositions { get; set; }
    }
}
