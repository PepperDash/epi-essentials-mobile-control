using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Devices.Common.TouchPanel
{
    public class MobileControlTouchpanelProperties : CrestronTouchpanelPropertiesConfig
    {
        [JsonProperty("useDirectServer")]
        public bool UseDirectServer { get; set; } = false;

        [JsonProperty("zoomRoomController")]
        public bool ZoomRoomController { get; set; } = false;


        /// <summary>
        /// Key of the device that this UI client will be running on. For example the key of a Cisco codec whose Navigator panel runs this UI client.
        /// </summary>
        [JsonProperty("deviceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string DeviceKey { get; set; }
    }
}