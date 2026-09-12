using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// A selectable selfview PiP position or size option, surfaced to the touchpanel so it can build a
    /// picker. <see cref="Command"/> is the value sent back to /setSelfviewPosition or /setSelfviewSize.
    /// </summary>
    public class SelfviewOption
    {
        /// <summary>
        /// The command value (sent back to the codec to select this option).
        /// </summary>
        [JsonProperty("command")]
        public string Command { get; set; }

        /// <summary>
        /// The human-readable label for display.
        /// </summary>
        [JsonProperty("label")]
        public string Label { get; set; }
    }
}
