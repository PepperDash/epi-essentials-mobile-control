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

#if SERIES4
        [JsonProperty("directServer")]
        public MobileControlDirectServerPropertiesConfig DirectServer { get; set; }

        [JsonProperty("enableApiServer")]
        public bool EnableApiServer { get; set; }
#endif

        [JsonProperty("roomBridges")]
        public List<MobileControlRoomBridgePropertiesConfig> RoomBridges { get; set; }

        public MobileControlConfig()
        {
            RoomBridges = new List<MobileControlRoomBridgePropertiesConfig>();

#if SERIES4
            EnableApiServer = true; // default to true
#endif
        }
    }

    public class MobileControlDirectServerPropertiesConfig
    {
        [JsonProperty("enableDirectServer")]
        public bool EnableDirectServer { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }
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