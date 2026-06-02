using System;

namespace PepperDash.Essentials.Plugins.Zoom.ZoomRoom
{
    public class ShareInfoEventArgs : EventArgs
    {
        public zStatus.Sharing SharingStatus { get; private set; }

        public ShareInfoEventArgs(zStatus.Sharing status)
        {
            SharingStatus = status;
        }
    }
}