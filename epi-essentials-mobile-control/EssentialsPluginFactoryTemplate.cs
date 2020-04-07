using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;

using Newtonsoft.Json;

namespace EssentialsPluginTemplateEPI
{
    public class EssentialsPluginFactoryTemplate : EssentialsPluginDeviceFactory<EssentialsPluginDeviceTemplate>
    {
        public EssentialsPluginFactoryTemplate()
        {
            // Set the minimum Essentials Framework Version
            MinimumEssentialsFrameworkVersion = "1.4.33";

            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "examplePluginDevice" };
        }

        // Builds and returns an instance of EssentialsPluginDeviceTemplate
        public override EssentialsDevice BuildDevice(PepperDash.Essentials.Core.Config.DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);

            var propertiesConfig = JsonConvert.DeserializeObject<EssentialsPluginConfigObjectTemplate>(dc.Properties.ToString());
            return new EssentialsPluginDeviceTemplate(dc.Key, dc.Name, propertiesConfig);
        }

    }
}