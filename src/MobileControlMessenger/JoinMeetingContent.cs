using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Inbound content for the /joinMeeting action: the meeting number to join and an optional passcode.
    /// </summary>
    public class JoinMeetingContent
    {
        [JsonProperty("meetingNumber")]
        public string MeetingNumber { get; set; }

        [JsonProperty("password", NullValueHandling = NullValueHandling.Ignore)]
        public string Password { get; set; }
    }
}
