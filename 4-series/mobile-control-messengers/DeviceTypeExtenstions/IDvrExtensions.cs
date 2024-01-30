using PepperDash.Essentials.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
using PepperDash.Essentials.AppServer;
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class DvrExtensions
    {
        public static void LinkActions(this IDvr dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", (dev as IKeyed).Key);

            controller.AddAction(prefix + "dvrlist", new PressAndHoldAction(dev.DvrList));
            controller.AddAction(prefix + "record", new PressAndHoldAction(dev.Record));
        }

        public static void UnlinkActions(this IDvr dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", (dev as IKeyed).Key);

            controller.RemoveAction(prefix + "dvrlist");
            controller.RemoveAction(prefix + "record");
        }
    }
}