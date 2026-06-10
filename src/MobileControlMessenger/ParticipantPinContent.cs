using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Inbound content for the participant pin/unpin actions: identifies the participant and the
    /// screen to pin them to.
    /// </summary>
    public class ParticipantPinContent
    {
        /// <summary>
        /// The user id of the participant to pin/unpin.
        /// </summary>
        [JsonProperty("userId")]
        public int UserId { get; set; }

        /// <summary>
        /// The screen index to pin the participant to.
        /// </summary>
        [JsonProperty("screenIndex")]
        public int ScreenIndex { get; set; }
    }
}
