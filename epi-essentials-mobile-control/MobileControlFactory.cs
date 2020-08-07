using System.Collections.Generic;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Room.MobileControl;

namespace PepperDash.Essentials
{
    public class MobileControlFactory : EssentialsPluginDeviceFactory<MobileControlSystemController>
    {
        public MobileControlFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.5.7";
            TypeNames = new List<string> {"appserver", "mobilecontrol"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var props = dc.Properties.ToObject<MobileControlConfig>();
            return new MobileControlSystemController(dc.Key, dc.Name, props);
        }
    }

    public class MobileControlDdvcFactory : EssentialsPluginDeviceFactory<MobileControlSystemController>
    {
        public MobileControlDdvcFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.6.1";
            TypeNames = new List<string> {"mobilecontrolbridge-ddvc01", "mobilecontrolbridge-simpl"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var comm = CommFactory.GetControlPropertiesConfig(dc);

            var bridge = new MobileControlSIMPLRoomBridge(dc.Key, dc.Name, comm.IpIdInt);
            bridge.AddPreActivationAction(() =>
            {
                var parent =
                    DeviceManager.AllDevices.FirstOrDefault(d => d.Key == "appServer") as MobileControlSystemController;
                if (parent == null)
                {
                    Debug.Console(0, bridge, "ERROR: Cannot connect bridge. System controller not present");
                    return;
                }
                Debug.Console(0, bridge, "Linking to parent controller");
                bridge.AddParent(parent);
                parent.AddBridge(bridge);
            });

            return bridge;
        }
    }
}