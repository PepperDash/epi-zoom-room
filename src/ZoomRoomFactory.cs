using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using Serilog.Events;

namespace PDT.Plugins.Zoom.Room
{
    public class ZoomRoomFactory : EssentialsPluginDeviceFactory<ZoomRoom>
    {
        public ZoomRoomFactory()
        {
            MinimumEssentialsFrameworkVersion = "3.0.0";
            TypeNames = new List<string> {"zoomroom"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.LogMessage(LogEventLevel.Information, "Factory Attempting to create new ZoomRoom Device");
            var comm = CommFactory.CreateCommForDevice(dc);
            return new ZoomRoom(dc, comm);
        }
    }
}