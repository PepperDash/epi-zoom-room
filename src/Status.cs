using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins
{
    public class Status
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
    }
}