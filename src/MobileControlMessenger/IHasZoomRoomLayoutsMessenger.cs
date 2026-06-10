using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core.Logging;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.Plugins;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for the Zoom-specific <see cref="PepperDash.Essentials.Plugins.IHasZoomRoomLayouts"/>:
    /// select a layout, page through participants, swap content/thumbnail, and push layout state. The base
    /// <c>IHasCodecLayouts</c> paths (cameraRemoteView/cameraLayout) are handled by the core layouts messenger.
    /// </summary>
    public class IHasZoomRoomLayoutsMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasZoomRoomLayoutsMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/selectLayout", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                if (string.IsNullOrEmpty(s?.Value))
                    return;

                if (Enum.TryParse<zConfiguration.eLayoutStyle>(s.Value, true, out var style))
                    _codec.SetLayout(style);
                else
                    this.LogWarning("/selectLayout: unrecognized layout '{Layout}' — ignoring", s.Value);
            });
            AddAction("/participantsNextPage", (id, content) => _codec.LayoutTurnNextPage());
            AddAction("/participantsPreviousPage", (id, content) => _codec.LayoutTurnPreviousPage());
            AddAction("/swapContentWithThumbnail", (id, content) => _codec.SwapContentWithThumbnail());
        }

        protected override bool CustomActivate()
        {
            _codec.LayoutInfoChanged += (s, e) =>
                Task.Run(() => PostStatusMessage(new ZoomRoomLayoutsStateMessage { Layouts = BuildLayoutState() }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new ZoomRoomLayoutsStateMessage { Layouts = BuildLayoutState() }, id));

        private ZoomRoomLayoutState BuildLayoutState() => new ZoomRoomLayoutState
        {
            AvailableLayouts = _codec.AvailableLayouts,
            LayoutViewIsOnFirstPage = _codec.LayoutViewIsOnFirstPageFeedback.BoolValue,
            LayoutViewIsOnLastPage = _codec.LayoutViewIsOnLastPageFeedback.BoolValue,
            CanSwapContentWithThumbnail = _codec.CanSwapContentWithThumbnailFeedback.BoolValue,
            ContentSwappedWithThumbnail = _codec.ContentSwappedWithThumbnailFeedback.BoolValue
        };
    }

    /// <summary>
    /// Status payload for <see cref="IHasZoomRoomLayoutsMessenger"/>.
    /// </summary>
    public class ZoomRoomLayoutsStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("layouts", NullValueHandling = NullValueHandling.Ignore)]
        public ZoomRoomLayoutState Layouts { get; set; }
    }

    /// <summary>
    /// Layout sub-state: available layouts, paging indicators, and content/thumbnail swap state.
    /// </summary>
    public class ZoomRoomLayoutState
    {
        [JsonProperty("availableLayouts", NullValueHandling = NullValueHandling.Ignore)]
        public zConfiguration.eLayoutStyle AvailableLayouts { get; set; }

        [JsonProperty("layoutViewIsOnFirstPage", NullValueHandling = NullValueHandling.Ignore)]
        public bool LayoutViewIsOnFirstPage { get; set; }

        [JsonProperty("layoutViewIsOnLastPage", NullValueHandling = NullValueHandling.Ignore)]
        public bool LayoutViewIsOnLastPage { get; set; }

        [JsonProperty("canSwapContentWithThumbnail", NullValueHandling = NullValueHandling.Ignore)]
        public bool CanSwapContentWithThumbnail { get; set; }

        [JsonProperty("contentSwappedWithThumbnail", NullValueHandling = NullValueHandling.Ignore)]
        public bool ContentSwappedWithThumbnail { get; set; }
    }
}
