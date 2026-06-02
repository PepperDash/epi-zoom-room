using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins.Zoom.Room
{
    public class Status
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
    }
}