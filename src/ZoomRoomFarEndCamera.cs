using PepperDash.Essentials.Devices.Common.Cameras;

namespace PDT.Plugins.Zoom.Room
{
    public class ZoomRoomFarEndCamera : ZoomRoomCamera, IAmFarEndCamera
    {

        public ZoomRoomFarEndCamera(string key, string name, ZoomRoom codec, int id)
            : base(key, name, codec)
        {
            Id = id;
        }

    }
}