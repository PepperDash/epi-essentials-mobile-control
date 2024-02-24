using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
#endif


namespace PepperDash.Essentials.Room.MobileControl
{
    public static class ColorExtensions
    {
        public static void LinkActions(this IColor dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.AddAction(prefix + "red", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Red(b)));
            controller.AddAction(prefix + "green", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Green(b)));
            controller.AddAction(prefix + "yellow", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Yellow(b)));
            controller.AddAction(prefix + "blue", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Blue(b)));
        }

        public static void UnlinkActions(this IColor dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.RemoveAction(prefix + "red");
            controller.RemoveAction(prefix + "green");
            controller.RemoveAction(prefix + "yellow");
            controller.RemoveAction(prefix + "blue");
        }
    }
}