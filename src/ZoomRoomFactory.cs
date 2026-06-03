using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using Serilog;
using Serilog.Events;

namespace PepperDash.Essentials.Plugins
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
            try
            {
                Log.Information("Factory: creating ZoomRoom device '{Key}'", dc.Key);

                var props = dc.Properties.ToObject<ZoomRoomPropertiesConfig>();
                var controller = new ZrcSdkController(
                    dc.Key + "-zrc",
                    props.SdkConfigPath,
                    props.ActivationCode);

                return new ZoomRoom(dc, controller);
            }
            catch (Exception ex)
            {
                // The Essentials DeviceFactory swallows build exceptions and only reports
                // "Cannot load unknown device type", so log the real cause explicitly here.
                Log.Error(ex, "Factory: failed to build ZoomRoom device '{Key}': {Message}", dc.Key, ex.Message);
                throw;
            }
        }
    }
}