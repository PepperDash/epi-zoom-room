using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Inbound content for the /savePreset action: the preset slot to store and an optional name.
    /// </summary>
    public class SavePresetContent
    {
        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }
    }
}
