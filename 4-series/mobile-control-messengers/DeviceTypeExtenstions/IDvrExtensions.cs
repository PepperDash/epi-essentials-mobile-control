using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class DvrExtensions
    {
        public static void LinkActions(this IDvr dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.AddAction(prefix + "dvrlist", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.DvrList(b)));
            controller.AddAction(prefix + "record", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Record(b)));
        }

        public static void UnlinkActions(this IDvr dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.RemoveAction(prefix + "dvrlist");
            controller.RemoveAction(prefix + "record");
        }
    }
}