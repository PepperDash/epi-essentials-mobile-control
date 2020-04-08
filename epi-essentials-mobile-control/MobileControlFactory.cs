using System.Collections.Generic;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Room.MobileControl;

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
            var comm = CommFactory.GetControlPropertiesConfig(dc);

            var bridge = new PepperDash.Essentials.Room.MobileControl.MobileControlSIMPLRoomBridge(dc.Key, dc.Name, comm.IpIdInt);
            bridge.AddPreActivationAction(() =>
            {
                var parent = DeviceManager.AllDevices.FirstOrDefault(d => d.Key == "appServer") as MobileControlSystemController;
                if (parent == null)
                {
                    Debug.Console(0, bridge, "ERROR: Cannot connect bridge. System controller not present");
                }
                Debug.Console(0, bridge, "Linking to parent controller");
                bridge.AddParent(parent);
                parent.AddBridge(bridge);
            });

            return bridge;
        }
    }
}