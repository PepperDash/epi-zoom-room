using System;

namespace PepperDash.Essentials.Plugins.Zoom.ZoomRoom
{
    public interface IZoomWirelessShareInstructions
    {
        event EventHandler<ShareInfoEventArgs> ShareInfoChanged;

        zStatus.Sharing SharingState { get; }
    }
}