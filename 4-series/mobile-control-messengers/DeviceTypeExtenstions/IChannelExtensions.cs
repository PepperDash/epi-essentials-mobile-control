using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class ChannelExtensions
    {
        public static void LinkActions(this IChannel dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.AddAction(prefix + "chanUp", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.ChannelUp(b)));

            controller.AddAction(prefix + "chanDown", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.ChannelDown(b)));
            controller.AddAction(prefix + "lastChan", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.LastChannel(b)));
            controller.AddAction(prefix + "guide", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Guide(b)));
            controller.AddAction(prefix + "info", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Info(b)));
            controller.AddAction(prefix + "exit", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Exit(b)));
        }

        public static void UnlinkActions(this IChannel dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.RemoveAction(prefix + "chanUp");
            controller.RemoveAction(prefix + "chanDown");
            controller.RemoveAction(prefix + "lastChan");
            controller.RemoveAction(prefix + "guide");
            controller.RemoveAction(prefix + "info");
            controller.RemoveAction(prefix + "exit");
        }


    }
}