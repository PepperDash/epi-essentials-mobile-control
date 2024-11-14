using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using System;
using System.Collections.Generic;

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
        public MobileControlApplicationConfig ApplicationConfig { get; set; }

        [JsonProperty("userInterfaceConfig")]
        public UserInterfaceConfig UserInterfaceConfig { get; set; }

        [JsonProperty("enableApiServer")]
        public bool EnableApiServer { get; set; }
#endif

        [JsonProperty("roomBridges")]
        [Obsolete("No longer necessary")]
        public List<MobileControlRoomBridgePropertiesConfig> RoomBridges { get; set; }

        public MobileControlConfig()
        {
            RoomBridges = new List<MobileControlRoomBridgePropertiesConfig>();

#if SERIES4
            EnableApiServer = true; // default to true
            ApplicationConfig = null;
            UserInterfaceConfig = null;
#endif
        }
    }

    public class MobileControlDirectServerPropertiesConfig
    {
        [JsonProperty("enableDirectServer")]
        public bool EnableDirectServer { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("logging")]
        public MobileControlLoggingConfig Logging { get; set; }

        public MobileControlDirectServerPropertiesConfig()
        {
            Logging = new MobileControlLoggingConfig();
        }
    }

    public class MobileControlLoggingConfig
    {
        [JsonProperty("enableRemoteLogging")]
        public bool EnableRemoteLogging { get; set; }

        [JsonProperty("host")]
        public string Host { get; set; }

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
        [JsonConverter(typeof(StringEnumConverter))]
        public MCIconSet? IconSet { get; set; }

        [JsonProperty("loginMode")]
        public string LoginMode { get; set; }

        [JsonProperty("modes")]
        public Dictionary<string, McMode> Modes { get; set; }

        [JsonProperty("enableRemoteLogging")]
        public bool Logging { get; set; }

    }

    public class UserInterfaceConfig
    {
        [JsonProperty("partnerMetadata", NullValueHandling = NullValueHandling.Ignore)]
        public List<MobileControlPartnerMetadata> PartnerMetadata { get; set; }

        [JsonProperty("techMenuConfig")]
        public TechMenuConfig TechMenuConfig { get; set; }

        [JsonProperty("customStyles")]
        public Dictionary<eUiModeKeys, JObject> CustomStyles { get; set; }

        public UserInterfaceConfig()
        {
            PartnerMetadata = new List<MobileControlPartnerMetadata>();
            TechMenuConfig = new TechMenuConfig();
            CustomStyles = new Dictionary<eUiModeKeys, JObject>();
        }
    }


    public class MobileControlPartnerMetadata
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("logoPath")]
        public string LogoPath { get; set; }
    }

    public class TechMenuConfig
    {
        [JsonProperty("leftNav")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Dictionary<eUiModeKeys, LeftNavItemConfig> LeftNav { get; set; }

        public TechMenuConfig()
        {
            LeftNav = new Dictionary<eUiModeKeys, LeftNavItemConfig>();
        }
    }

    public enum eUiModeKeys
    {
        systemStatus,
        matrixRouting,
        displays,
        audio,
        setTopBox,
        environment,
        roomSchedule,
        roomSetup,
        changePin,
        about,
    }

    public class LeftNavItemConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }
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