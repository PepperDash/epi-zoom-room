using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

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
            if (dc.Properties == null)
                throw new InvalidOperationException($"ZoomRoom device '{dc.Key}' is missing a Properties object in config.");

            var props = dc.Properties.ToObject<ZoomRoomPropertiesConfig>();

            // NOTE: the factory is not IKeyed and this plugin loads in its own context, so the
            // static Serilog `Log` here writes to a dead per-plugin logger that never reaches the
            // Essentials log — a build exception then surfaces only as the generic "Cannot load
            // unknown device type". The controller IS IKeyed, so once it exists we log failures
            // through it; failures while constructing the controller itself are logged via its own
            // IKeyed channel (see ZrcSdkController ctor).
            var controller = new ZrcSdkController(
                dc.Key + "-zrc",
                props.SdkConfigPath,
                props.ActivationCode);

            try
            {
                return new ZoomRoom(dc, controller, props);
            }
            catch (Exception ex)
            {
                controller.LogError(ex, "Factory: failed to build ZoomRoom device '{Key}': {Message}", dc.Key, ex.Message);
                throw;
            }
        }
    }
}