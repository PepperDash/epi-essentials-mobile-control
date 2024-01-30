using PepperDash.Essentials.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
using PepperDash.Essentials.AppServer;
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class SetTopBoxControlsExtensions
    {
        public static void LinkActions(this ISetTopBoxControls dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.AddAction(prefix + "dvrList", new PressAndHoldAction(dev.DvrList));
            controller.AddAction(prefix + "replay", new PressAndHoldAction(dev.Replay));
        }

        public static void UnlinkActions(this ISetTopBoxControls dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.RemoveAction(prefix + "dvrList");
            controller.RemoveAction(prefix + "replay");
        }
    }
}