using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Devices.Common.Codec;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Inbound content for the invite-contacts actions: the contacts to invite and (for a new meeting)
    /// the meeting duration.
    /// </summary>
    public class Invitation
    {
        [JsonProperty("duration")]
        public uint Duration { get; set; }
        [JsonProperty("invitees")]
        public List<InvitableDirectoryContact> Invitees { get; set; }
    }
}
