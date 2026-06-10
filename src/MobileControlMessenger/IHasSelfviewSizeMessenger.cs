using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    // ── Size messenger ──────────────────────────────────────────────────────────

    /// <summary>
    /// Mobile Control messenger for selfview PiP size.
    /// Reuses <see cref="SelfviewOptionMessengerBase"/> — identical logic to
    /// <see cref="IHasSelfviewPositionMessenger"/> with size-specific paths and feedback (#23).
    /// </summary>
    public class IHasSelfviewSizeMessenger : SelfviewOptionMessengerBase
    {
        public IHasSelfviewSizeMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec) { }

        protected override string ToggleAction  => "/toggleSelfviewSize";
        protected override string SetAction     => "/setSelfviewSize";
        protected override void ExecuteToggle() => _codec.SelfviewPipSizeToggle();
        protected override void ExecuteSet(CodecCommandWithLabel cmd) => _codec.SelfviewPipSizeSet(cmd);
        protected override StringFeedback GetFeedback()               => _codec.SelfviewPipSizeFeedback;
        protected override IEnumerable<CodecCommandWithLabel> GetOptions() => _codec.SelfviewPipSizes ?? Enumerable.Empty<CodecCommandWithLabel>();

        protected override DeviceStateMessageBase BuildFullStatus() =>
            new SelfviewSizeStateMessage
            {
                SelfviewPipSize  = _codec.SelfviewPipSizeFeedback.StringValue,
                AvailableSizes   = GetOptions().Select(o => new SelfviewOption { Command = o.Command, Label = o.Label }).ToList()
            };

        protected override DeviceStateMessageBase BuildChangedStatus(string newValue) =>
            new SelfviewSizeStateMessage { SelfviewPipSize = newValue };
    }

    /// <summary>Status payload for <see cref="IHasSelfviewSizeMessenger"/>.</summary>
    public class SelfviewSizeStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("selfviewPipSize", NullValueHandling = NullValueHandling.Ignore)]
        public string SelfviewPipSize { get; set; }

        [JsonProperty("availableSizes", NullValueHandling = NullValueHandling.Ignore)]
        public List<SelfviewOption> AvailableSizes { get; set; }
    }
}
