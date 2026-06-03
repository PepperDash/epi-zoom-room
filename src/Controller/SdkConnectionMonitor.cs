using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins
{
    /// <summary>
    /// Minimal StatusMonitorBase backed by SDK connection state events.
    /// Online/offline state is driven externally via the IsOnline property.
    /// </summary>
    public class SdkConnectionMonitor : StatusMonitorBase
    {
        public SdkConnectionMonitor(IKeyed parent)
            : base(parent, 30000, 60000)
        {
        }

        public override void Start()
        {
            // No polling — state is set externally via IsOnline
        }

        public override void Stop()
        {
            // No polling to stop
        }

        /// <summary>
        /// Called by ZoomRoom when SDK connection state changes.
        /// </summary>
        public void SetOnline(bool online)
        {
            IsOnline = online;
            Status   = online ? MonitorStatus.IsOk : MonitorStatus.InError;
        }
    }
}
