using System.Threading.Tasks;
using Newtonsoft.Json;
using ZoomRoomDevice = PepperDash.Essentials.Plugins.ZoomRoom;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Mobile Control messenger for <see cref="PepperDash.Essentials.Devices.Common.Cameras.IHasCameraAutoMode"/>:
    /// enable / disable / toggle camera auto (smart) mode, plus its on/off status.
    /// </summary>
    public class IHasCameraAutoModeMessenger : MessengerBase
    {
        private readonly ZoomRoomDevice _codec;

        public IHasCameraAutoModeMessenger(string key, string messagePath, ZoomRoomDevice codec)
            : base(key, messagePath, codec)
        {
            _codec = codec;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendFullStatus(id));

            AddAction("/cameraAutoModeOn", (id, content) => _codec.CameraAutoModeOn());
            AddAction("/cameraAutoModeOff", (id, content) => _codec.CameraAutoModeOff());
            AddAction("/cameraAutoModeToggle", (id, content) => _codec.CameraAutoModeToggle());
        }

        protected override bool CustomActivate()
        {
            _codec.CameraAutoModeIsOnFeedback.OutputChange += (s, e) =>
                Task.Run(() => PostStatusMessage(new CameraAutoModeStateMessage { CameraAutoModeIsOn = e.BoolValue }));

            return base.CustomActivate();
        }

        private void SendFullStatus(string id = null) =>
            Task.Run(() => PostStatusMessage(
                new CameraAutoModeStateMessage { CameraAutoModeIsOn = _codec.CameraAutoModeIsOnFeedback.BoolValue }, id));
    }

    /// <summary>
    /// Status payload for <see cref="IHasCameraAutoModeMessenger"/>.
    /// </summary>
    public class CameraAutoModeStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("cameraAutoModeIsOn", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraAutoModeIsOn { get; set; }
    }
}
