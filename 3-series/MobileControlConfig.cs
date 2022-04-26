﻿using System.Collections.Generic;
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

        [JsonProperty("applicationConfig")]
        public MobileControlApplicationConfig ApplicationConfig{get; set;}

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
            ApplicationConfig = null;                 
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

    public class MobileControlApplicationConfig
    {
        [JsonProperty("apiPath")]
        public string ApiPath { get; set; }

        [JsonProperty("gatewayAppPath")]
        public string GatewayAppPath { get; set; }

        [JsonProperty("enableDev")]
        public bool? EnableDev { get; set; }

        [JsonProperty("logoPath")]
        public string LogoPath { get; set; }

        [JsonProperty("iconSet")]
        public MCIconSet? IconSet { get; set; }

        [JsonProperty("loginMode")]
        public string LoginMode { get; set; }

        [JsonProperty("modes")]
        public Dictionary<string, McMode> Modes { get; set; }
    }

    public class McMode
    {
        [JsonProperty("listPageText")]
        public string ListPageText { get; set; }
        [JsonProperty("loginHelpText")]
        public string LoginHelpText { get; set; }

        [JsonProperty("passcodePageText")]
        public string PasscodePageText { get; set; }
    }

    public enum MCIconSet
    {
        GOOGLE,
        HABANERO,
        NEO
    }
}