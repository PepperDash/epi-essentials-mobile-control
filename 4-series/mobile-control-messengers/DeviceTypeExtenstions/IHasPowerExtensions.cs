using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.Room.MobileControl
{
    public static class HasPowerExtensions
    {
        public static void LinkActions(this IHasPowerControl dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.AddAction(prefix + "powerOn", (id, content) => dev.PowerOn());
            controller.AddAction(prefix + "powerOff", (id, content) => dev.PowerOff());
            controller.AddAction(prefix + "powerToggle", (id, content) => dev.PowerToggle());
        }

        public static void UnlinkActions(this IHasPowerControl dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.RemoveAction(prefix + "powerOn");
            controller.RemoveAction(prefix + "powerOff");
            controller.RemoveAction(prefix + "powerToggle");
        }
    }
}