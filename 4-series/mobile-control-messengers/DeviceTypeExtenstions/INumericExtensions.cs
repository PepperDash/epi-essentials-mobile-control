using PepperDash.Essentials.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.AppServer.Messengers;
#if SERIES4
using PepperDash.Essentials.AppServer;
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class NumericExtensions
    {
        public static void LinkActions(this INumericKeypad dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.AddAction(prefix + "num0", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit0(b)));
            controller.AddAction(prefix + "num1", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit1(b)));
            controller.AddAction(prefix + "num2", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit2(b)));
            controller.AddAction(prefix + "num3", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit3(b)));
            controller.AddAction(prefix + "num4", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit4(b)));
            controller.AddAction(prefix + "num5", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit5(b)));
            controller.AddAction(prefix + "num6", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit6(b)));
            controller.AddAction(prefix + "num7", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit7(b)));
            controller.AddAction(prefix + "num8", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit8(b)));
            controller.AddAction(prefix + "num9", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Digit9(b)));
            controller.AddAction(prefix + "numDash", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.KeypadAccessoryButton1(b)));
            controller.AddAction(prefix + "numEnter", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.KeypadAccessoryButton2(b)));
            // Deal with the Accessory functions on the numpad later
        }

        public static void UnlinkActions(this INumericKeypad dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.RemoveAction(prefix + "num0");
            controller.RemoveAction(prefix + "num1");
            controller.RemoveAction(prefix + "num2");
            controller.RemoveAction(prefix + "num3");
            controller.RemoveAction(prefix + "num4");
            controller.RemoveAction(prefix + "num5");
            controller.RemoveAction(prefix + "num6");
            controller.RemoveAction(prefix + "num7");
            controller.RemoveAction(prefix + "num8");
            controller.RemoveAction(prefix + "num9");
            controller.RemoveAction(prefix + "numDash");
            controller.RemoveAction(prefix + "numEnter");
        }
    }
}