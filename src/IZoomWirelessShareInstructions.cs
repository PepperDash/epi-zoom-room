using System;

namespace PDT.Plugins.Zoom.Room
{
    public interface IZoomWirelessShareInstructions
    {
        event EventHandler<ShareInfoEventArgs> ShareInfoChanged;

        zStatus.Sharing SharingState { get; }
    }
}