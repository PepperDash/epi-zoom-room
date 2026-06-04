using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins
{
    public class ZoomRoomPropertiesConfig
    {
        [JsonProperty("communicationMonitorProperties")]
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }

        [JsonProperty("disablePhonebookAutoDownload")]
        public bool DisablePhonebookAutoDownload { get; set; }

        [JsonProperty("supportsCameraAutoMode")]
        public bool SupportsCameraAutoMode { get; set; }

        [JsonProperty("supportsCameraOff")]
        public bool SupportsCameraOff { get; set; }

        //if true, the layouts will be set automatically when sharing starts/ends or a call is joined
        [JsonProperty("autoDefaultLayouts")]
        public bool AutoDefaultLayouts { get; set; }

        /* This layout will be selected when Sharing starts (either from Far end or locally)*/
        [JsonProperty("defaultSharingLayout")]
        [JsonConverter(typeof(StringEnumConverter))]
        public zConfiguration.eLayoutStyle DefaultSharingLayout { get; set; }

        //This layout will be selected when a call is connected and no content is being shared
        [JsonProperty("defaultCallLayout")]
        [JsonConverter(typeof(StringEnumConverter))]
        public zConfiguration.eLayoutStyle DefaultCallLayout { get; set; }

        [JsonProperty("minutesBeforeMeetingStart")]
        public int MinutesBeforeMeetingStart { get; set; }

        [JsonProperty("activationCode")]
        public string ActivationCode { get; set; }

        /// <summary>
        /// How <c>DialPhoneCall</c> places outbound calls. <c>Sip</c> (default) uses the SDK's SIP
        /// phone service; <c>Pstn</c> uses PSTN call-out (not yet implemented).
        /// </summary>
        [JsonProperty("phoneDialMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ePhoneDialMode PhoneDialMode { get; set; } = ePhoneDialMode.Sip;

        /// <summary>
        /// Path passed to ZrcSdk.Initialize(). Defaults to /user/zrcsdk (writable, persistent volume).
        /// </summary>
        [JsonProperty("sdkConfigPath")]
        public string SdkConfigPath { get; set; } = "/user/zrcsdk";

        // communicationMonitorProperties is kept for back-compat but is no longer used —
        // the SDK is event-driven and requires no polling.
    }

    /// <summary>Outbound phone dialing transport for <c>DialPhoneCall</c>.</summary>
    public enum ePhoneDialMode
    {
        /// <summary>SIP phone service (default).</summary>
        Sip,
        /// <summary>PSTN call-out (not yet implemented).</summary>
        Pstn,
    }
}