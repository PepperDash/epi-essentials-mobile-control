using PepperDash.Essentials.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
using PepperDash.Essentials.AppServer;
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class ChannelExtensions
    {
        public static void LinkActions(this IChannel dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.AddAction(prefix + "chanUp", new PressAndHoldAction(dev.ChannelUp));
            controller.AddAction(prefix + "chanDown", new PressAndHoldAction(dev.ChannelDown));
            controller.AddAction(prefix + "lastChan", new PressAndHoldAction(dev.LastChannel));
            controller.AddAction(prefix + "guide", new PressAndHoldAction(dev.Guide));
            controller.AddAction(prefix + "info", new PressAndHoldAction(dev.Info));
            controller.AddAction(prefix + "exit", new PressAndHoldAction(dev.Exit));
        }

        public static void UnlinkActions(this IChannel dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.RemoveAction(prefix + "chanUp");
            controller.RemoveAction(prefix + "chanDown");
            controller.RemoveAction(prefix + "lastChan");
            controller.RemoveAction(prefix + "guide");
            controller.RemoveAction(prefix + "info");
            controller.RemoveAction(prefix + "exit");
        }
    }
}