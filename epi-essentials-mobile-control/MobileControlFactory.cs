using System.Collections.Generic;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Room.MobileControl;

namespace PepperDash.Essentials
{
    public class MobileControlFactory : EssentialsPluginDeviceFactory<MobileControlSystemController>
    {
        public MobileControlFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.7.3";
            TypeNames = new List<string> {"appserver", "mobilecontrol", "webserver" };
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
            MinimumEssentialsFrameworkVersion = "1.7.3";
            TypeNames = new List<string> {"mobilecontrolbridge-ddvc01", "mobilecontrolbridge-simpl"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var comm = CommFactory.GetControlPropertiesConfig(dc);

            var bridge = new MobileControlSIMPLRoomBridge(dc.Key, dc.Name, comm.IpIdInt);

            bridge.AddPreActivationAction(() =>
            {
                var parent = GetMobileControlDevice();

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

        private static MobileControlSystemController GetMobileControlDevice()
        {
            var mobileControlList = DeviceManager.AllDevices.OfType<MobileControlSystemController>().ToList();

            if (mobileControlList.Count > 1)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Warning,
                    "Multiple instances of Mobile Control Server found.");
                return null;
            }

            if (mobileControlList.Count > 0)
            {
                return mobileControlList[0];
            }

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Mobile Control not enabled for this system");
            return null;
        }
    }
}