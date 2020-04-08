using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Core.JsonStandardObjects;
using PepperDash.Essentials.Core;
using DeviceConfig = PepperDash.Essentials.Core.Config.DeviceConfig;

namespace PepperDash.Essentials
{
    public class MobileControlFactory:EssentialsDeviceFactory<MobileControlSystemController>
    {
        public MobileControlFactory()
        {
            TypeNames = new List<string> {"appServer", "mobileControl"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var props = dc.Properties.ToObject<MobileControlConfig>();
            return new MobileControlSystemController(dc.Key, dc.Name, props);
        }
    }

    public class MobileControlDdvcFactory : EssentialsDeviceFactory<MobileControlSystemController>
    {
        public MobileControlDdvcFactory()
        {
            TypeNames = new List<string> {"mobilecontrolbridge-ddvc01"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            throw new System.NotImplementedException();
        }
    }
}