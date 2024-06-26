﻿using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;


namespace PepperDash.Essentials
{
    /// <summary>
    /// Used to overlay additional config data from mobile control on
    /// </summary>
    public class MobileControlEssentialsConfig : EssentialsConfig
    {
        [JsonProperty("runtimeInfo")]
        public MobileControlRuntimeInfo RuntimeInfo { get; set; }

        public MobileControlEssentialsConfig(EssentialsConfig config)
            : base()
        {
            // TODO: Consider using Reflection to iterate properties
            this.Devices = config.Devices;
            this.Info = config.Info;
            this.JoinMaps = config.JoinMaps;
            this.Rooms = config.Rooms;
            this.SourceLists = config.SourceLists;
            this.DestinationLists = config.DestinationLists;
            this.SystemUrl = config.SystemUrl;
            this.TemplateUrl = config.TemplateUrl;
            this.TieLines = config.TieLines;

            if (this.Info == null)
                this.Info = new InfoConfig();

            RuntimeInfo = new MobileControlRuntimeInfo();
        }
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