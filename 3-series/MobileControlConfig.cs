using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials
{
    /// <summary>
    /// 
    /// </summary>
    public class MobileControlConfig
    {
        [JsonProperty("serverUrl")]
        public string ServerUrl { get; set; }

        [JsonProperty("clientAppUrl")]
        public string ClientAppUrl { get; set; }

        [JsonProperty("roomBridges")]
        public List<MobileControlRoomBridgePropertiesConfig> RoomBridges { get; set; }

        public MobileControlConfig()
        {
            RoomBridges = new List<MobileControlRoomBridgePropertiesConfig>();
        }
    }

    public class MobileControlRoomBridgePropertiesConfig
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("roomKey")]
        public string RoomKey { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class MobileControlSimplRoomBridgePropertiesConfig
    {
        [JsonProperty("eiscId")]
        public string EiscId { get; set; }
    }
}