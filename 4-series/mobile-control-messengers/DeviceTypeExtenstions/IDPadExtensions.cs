using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.AppServer.Messengers;
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

            controller.AddAction(prefix + "up", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Up(b)));
            controller.AddAction(prefix + "down", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Down(b)));
            controller.AddAction(prefix + "left", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Left(b)));
            controller.AddAction(prefix + "right", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Right(b)));
            controller.AddAction(prefix + "select", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Select(b)));
            controller.AddAction(prefix + "menu", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Menu(b)));
            controller.AddAction(prefix + "exit", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Exit(b)));
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