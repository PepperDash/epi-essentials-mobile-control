using PepperDash.Essentials.Core;
using PepperDash.Core;

namespace PepperDash.Essentials.Room.MobileControl
{
    public static class ColorExtensions
    {
        public static void LinkActions(this IColor dev, MobileControlSystemController controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.AddAction(prefix + "red", new PressAndHoldAction(dev.Red));
            controller.AddAction(prefix + "green", new PressAndHoldAction(dev.Green));
            controller.AddAction(prefix + "yellow", new PressAndHoldAction(dev.Yellow));
            controller.AddAction(prefix + "blue", new PressAndHoldAction(dev.Blue));
        }

        public static void UnlinkActions(this IColor dev, MobileControlSystemController controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.RemoveAction(prefix + "red");
            controller.RemoveAction(prefix + "green");
            controller.RemoveAction(prefix + "yellow");
            controller.RemoveAction(prefix + "blue");
        }
    }
}