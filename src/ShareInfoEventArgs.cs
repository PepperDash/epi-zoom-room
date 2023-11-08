using System;

namespace PDT.Plugins.Zoom.Room
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