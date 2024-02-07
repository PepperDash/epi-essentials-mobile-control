using PepperDash.Essentials.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.AppServer.Messengers;
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

            controller.AddAction(prefix + "dvrList", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.DvrList(b)));
            controller.AddAction(prefix + "replay", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Replay(b)));
        }

        public static void UnlinkActions(this ISetTopBoxControls dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.RemoveAction(prefix + "dvrList");
            controller.RemoveAction(prefix + "replay");
        }
    }
}