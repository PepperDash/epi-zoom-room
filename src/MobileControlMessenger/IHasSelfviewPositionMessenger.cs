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
    /// Parameterized base for selfview PiP option messengers (position and size share identical logic).
    /// Subclasses supply the action paths, feedback binding, and JSON property names; the base handles
    /// /fullStatus, /toggle, /set, and the OutputChange subscription (#23).
    /// </summary>
    public abstract class SelfviewOptionMessengerBase : MessengerBase
    {
        protected readonly ZoomRoomDevice _codec;

        protected SelfviewOptionMessengerBase(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        // Subclass contract
        protected abstract string ToggleAction { get; }
        protected abstract string SetAction { get; }
        protected abstract void ExecuteToggle();
        protected abstract void ExecuteSet(CodecCommandWithLabel cmd);
        protected abstract StringFeedback GetFeedback();
        protected abstract IEnumerable<CodecCommandWithLabel> GetOptions();
        protected abstract DeviceStateMessageBase BuildFullStatus();
        protected abstract DeviceStateMessageBase BuildChangedStatus(string newValue);

        protected override void RegisterActions()
        {
            base.RegisterActions();
            AddAction("/fullStatus", (id, content) => SendFullStatus(id));
            AddAction(ToggleAction, (id, content) => ExecuteToggle());
            AddAction(SetAction, (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                var cmd = FindOption(s?.Value);
                if (cmd != null) ExecuteSet(cmd);
            });
        }

        protected override bool CustomActivate()
        {
            GetFeedback().OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(BuildChangedStatus(e.StringValue)));
            return base.CustomActivate();
        }

        protected CodecCommandWithLabel FindOption(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var options = GetOptions();
            return options.FirstOrDefault(o => string.Equals(o.Command, value, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault(o => string.Equals(o.Label, value, StringComparison.OrdinalIgnoreCase));
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(BuildFullStatus(), id));
    }

    // ── Position messenger ──────────────────────────────────────────────────────

    /// <summary>
    /// Mobile Control messenger for selfview PiP position.
    /// </summary>
    public class IHasSelfviewPositionMessenger : SelfviewOptionMessengerBase
    {
        public IHasSelfviewPositionMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec) { }

        protected override string ToggleAction  => "/toggleSelfviewPosition";
        protected override string SetAction     => "/setSelfviewPosition";
        protected override void ExecuteToggle() => _codec.SelfviewPipPositionToggle();
        protected override void ExecuteSet(CodecCommandWithLabel cmd) => _codec.SelfviewPipPositionSet(cmd);
        protected override StringFeedback GetFeedback()               => _codec.SelfviewPipPositionFeedback;
        protected override IEnumerable<CodecCommandWithLabel> GetOptions() => _codec.SelfviewPipPositions ?? Enumerable.Empty<CodecCommandWithLabel>();

        protected override DeviceStateMessageBase BuildFullStatus() =>
            new SelfviewPositionStateMessage
            {
                SelfviewPipPosition = _codec.SelfviewPipPositionFeedback.StringValue,
                AvailablePositions  = GetOptions().Select(o => new SelfviewOption { Command = o.Command, Label = o.Label }).ToList()
            };

        protected override DeviceStateMessageBase BuildChangedStatus(string newValue) =>
            new SelfviewPositionStateMessage { SelfviewPipPosition = newValue };
    }

    /// <summary>Status payload for <see cref="IHasSelfviewPositionMessenger"/>.</summary>
    public class SelfviewPositionStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("selfviewPipPosition", NullValueHandling = NullValueHandling.Ignore)]
        public string SelfviewPipPosition { get; set; }

        [JsonProperty("availablePositions", NullValueHandling = NullValueHandling.Ignore)]
        public List<SelfviewOption> AvailablePositions { get; set; }
    }
}
