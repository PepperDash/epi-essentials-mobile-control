using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core.Config;
using Newtonsoft.Json;


namespace PepperDash.Essentials
{
    /// <summary>
    /// Used to overlay additional config data from mobile control on
    /// </summary>
    public class MobileControlEssentialsConfig : EssentialsConfig
    {
        [JsonProperty("runtimeInfo")]
        public MobileControlRuntimeInfo RuntimeInfo { get; set; }
    }

    /// <summary>
    /// Used to add any additional runtime information from mobile control to be send to the API
    /// </summary>
    public class MobileControlRuntimeInfo
    {
        [JsonProperty("pluginVersion")]
        public string PluginVersion { get; set; }
    }
}