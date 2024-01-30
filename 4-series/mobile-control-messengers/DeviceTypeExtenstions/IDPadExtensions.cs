using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
using PepperDash.Essentials.AppServer;
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class IdPadExtensions
    {
        public static void LinkActions(this IDPad dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", (dev as IKeyed).Key);

            controller.AddAction(prefix + "up", new PressAndHoldAction(dev.Up));
            controller.AddAction(prefix + "down", new PressAndHoldAction(dev.Down));
            controller.AddAction(prefix + "left", new PressAndHoldAction(dev.Left));
            controller.AddAction(prefix + "right", new PressAndHoldAction(dev.Right));
            controller.AddAction(prefix + "select", new PressAndHoldAction(dev.Select));
            controller.AddAction(prefix + "menu", new PressAndHoldAction(dev.Menu));
            controller.AddAction(prefix + "exit", new PressAndHoldAction(dev.Exit));
        }

        public static void UnlinkActions(this IDPad dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", (dev as IKeyed).Key);

            controller.RemoveAction(prefix + "up");
            controller.RemoveAction(prefix + "down");
            controller.RemoveAction(prefix + "left");
            controller.RemoveAction(prefix + "right");
            controller.RemoveAction(prefix + "select");
            controller.RemoveAction(prefix + "menu");
            controller.RemoveAction(prefix + "exit");
        }
    }
}