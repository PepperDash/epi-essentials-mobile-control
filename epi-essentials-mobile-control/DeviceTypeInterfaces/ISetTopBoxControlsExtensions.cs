using PepperDash.Essentials.Core;
using PepperDash.Core;

namespace PepperDash.Essentials.Room.MobileControl
{
    public static class SetTopBoxControlsExtensions
    {
        public static void LinkActions(this ISetTopBoxControls dev, MobileControlSystemController controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.AddAction(prefix + "dvrList", new PressAndHoldAction(dev.DvrList));
            controller.AddAction(prefix + "replay", new PressAndHoldAction(dev.Replay));
        }

        public static void UnlinkActions(this ISetTopBoxControls dev, MobileControlSystemController controller)
        {
            var prefix = string.Format(@"/device/{0}/", dev.Key);

            controller.RemoveAction(prefix + "dvrList");
            controller.RemoveAction(prefix + "replay");
        }
    }
}