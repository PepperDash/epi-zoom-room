using Newtonsoft.Json.Linq;
using PepperDash.Essentials.AppServer;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Recall/save actions for camera presets (<see cref="PepperDash.Essentials.Devices.Common.VideoCodec.IHasCodecRoomPresets"/>).
    /// The preset LIST (status) is published by the core <c>IHasCodecRoomPresetsMessenger</c> (auto-registered);
    /// the core messenger is status-only, so this adds the inbound actions it lacks. Distinct paths
    /// (/recallPreset, /savePreset) — no collision with the core messenger's /fullStatus, /roomPresetsStatus.
    /// </summary>
    public class IHasCodecRoomPresetsActionsMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasCodecRoomPresetsActionsMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/recallPreset", (id, content) =>
            {
                var m = content?.ToObject<MobileControlSimpleContent<int>>();
                if (m != null) _codec.CodecRoomPresetSelect(m.Value);
            });
            AddAction("/savePreset", (id, content) =>
            {
                var m = content?.ToObject<SavePresetContent>();
                if (m != null) _codec.CodecRoomPresetStore(m.Index, m.Description);
            });
        }
    }
}
