using System.Threading.Tasks;
using Newtonsoft.Json;
using PepperDash.Essentials.Plugins;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for the Zoom-specific <see cref="PepperDash.Essentials.Plugins.IZoomWirelessShareInstructions"/>:
    /// pushes wireless-sharing status. Status-only (the interface exposes no inbound commands).
    /// </summary>
    public class IZoomWirelessShareInstructionsMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IZoomWirelessShareInstructionsMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));
        }

        protected override bool CustomActivate()
        {
            _codec.ShareInfoChanged += (s, e) =>
                Task.Run(() => PostStatusMessage(new ShareInfoStateMessage { ShareInfo = e.SharingStatus }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new ShareInfoStateMessage { ShareInfo = _codec.SharingState }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IZoomWirelessShareInstructionsMessenger"/>.
    /// </summary>
    public class ShareInfoStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("shareInfo", NullValueHandling = NullValueHandling.Ignore)]
        public zStatus.Sharing ShareInfo { get; set; }
    }
}
