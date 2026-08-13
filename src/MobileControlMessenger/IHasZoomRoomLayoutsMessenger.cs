using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
            AddAction("/setVideoOrder", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                if (string.IsNullOrEmpty(s?.Value))
                    return;

                _codec.SetVideoOrder(s.Value);
            });
            AddAction("/setThumbnailsPosition", (id, content) =>
            {
                var s = content?.ToObject<MobileControlSimpleContent<string>>();
                if (string.IsNullOrEmpty(s?.Value))
                    return;

                _codec.SetThumbnailsPosition(s.Value);
            });

            _codec.LayoutInfoChanged += (s, e) =>
                Task.Run(() => PostStatusMessage(new ZoomRoomLayoutsStateMessage { Layouts = BuildLayoutState() }));
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(new ZoomRoomLayoutsStateMessage { Layouts = BuildLayoutState() }, id));

        private ZoomRoomLayoutState BuildLayoutState() => new ZoomRoomLayoutState
        {
            AvailableLayouts = GetAvailableLayoutOptions(),
            LayoutViewIsOnFirstPage = _codec.LayoutViewIsOnFirstPageFeedback.BoolValue,
            LayoutViewIsOnLastPage = _codec.LayoutViewIsOnLastPageFeedback.BoolValue,
            CanSwapContentWithThumbnail = _codec.CanSwapContentWithThumbnailFeedback.BoolValue,
            ContentSwappedWithThumbnail = _codec.ContentSwappedWithThumbnailFeedback.BoolValue,
            VideoOrder = _codec.CurrentVideoOrder,
            AvailableVideoOrders = GetAvailableVideoOrders(),
            ThumbnailsPosition = _codec.CurrentThumbnailsPosition,
            AvailableThumbnailsPositions = GetAvailableThumbnailsPositions()
        };

        private static readonly Dictionary<zConfiguration.eLayoutStyle, string> LayoutDisplayLabels = new()
        {
            { zConfiguration.eLayoutStyle.Gallery, "Gallery" },
            { zConfiguration.eLayoutStyle.Speaker, "Speaker" },
            { zConfiguration.eLayoutStyle.Thumbnail, "Thumbnail" },
            { zConfiguration.eLayoutStyle.ContentOnly, "Shared Content" },
            { zConfiguration.eLayoutStyle.CancelContentOnly, "Cancel Shared Content" },
            { zConfiguration.eLayoutStyle.Dynamic, "Dynamic Gallery" },
            { zConfiguration.eLayoutStyle.MultiSpeaker, "Multi-Speaker" },
            { zConfiguration.eLayoutStyle.ThumbnailAndShare, "Thumbnail & Share" },
        };

        private List<LayoutOption> GetAvailableLayoutOptions()
        {
            var layouts = _codec.AvailableLayouts;
            var options = new List<LayoutOption>();

            foreach (zConfiguration.eLayoutStyle style in Enum.GetValues(typeof(zConfiguration.eLayoutStyle)))
            {
                if (style == zConfiguration.eLayoutStyle.None)
                    continue;

                if (!layouts.HasFlag(style))
                    continue;

                options.Add(new LayoutOption
                {
                    Command = style.ToString(),
                    Label = LayoutDisplayLabels.TryGetValue(style, out var label) ? label : style.ToString()
                });
            }

            return options;
        }

        private List<LayoutOption> GetAvailableVideoOrders() => new()
        {
            new LayoutOption { Command = "Default", Label = "Default" },
            new LayoutOption { Command = "Alphabetical", Label = "Alphabetical" },
            new LayoutOption { Command = "ReverseAlphabetical", Label = "Reverse Alphabetical" }
        };

        private List<LayoutOption> GetAvailableThumbnailsPositions() => new()
        {
            new LayoutOption { Command = "Top", Label = "Top" },
            new LayoutOption { Command = "Bottom", Label = "Bottom" }
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
        public List<LayoutOption> AvailableLayouts { get; set; }

        [JsonProperty("layoutViewIsOnFirstPage", NullValueHandling = NullValueHandling.Ignore)]
        public bool LayoutViewIsOnFirstPage { get; set; }

        [JsonProperty("layoutViewIsOnLastPage", NullValueHandling = NullValueHandling.Ignore)]
        public bool LayoutViewIsOnLastPage { get; set; }

        [JsonProperty("canSwapContentWithThumbnail", NullValueHandling = NullValueHandling.Ignore)]
        public bool CanSwapContentWithThumbnail { get; set; }

        [JsonProperty("contentSwappedWithThumbnail", NullValueHandling = NullValueHandling.Ignore)]
        public bool ContentSwappedWithThumbnail { get; set; }

        [JsonProperty("videoOrder", NullValueHandling = NullValueHandling.Ignore)]
        public string VideoOrder { get; set; }

        [JsonProperty("availableVideoOrders", NullValueHandling = NullValueHandling.Ignore)]
        public List<LayoutOption> AvailableVideoOrders { get; set; }

        [JsonProperty("thumbnailsPosition", NullValueHandling = NullValueHandling.Ignore)]
        public string ThumbnailsPosition { get; set; }

        [JsonProperty("availableThumbnailsPositions", NullValueHandling = NullValueHandling.Ignore)]
        public List<LayoutOption> AvailableThumbnailsPositions { get; set; }
    }

    /// <summary>
    /// Represents a single available layout option with a command and display label.
    /// </summary>
    public class LayoutOption
    {
        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }
    }
}
