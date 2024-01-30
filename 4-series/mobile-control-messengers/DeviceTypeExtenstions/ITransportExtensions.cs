using PepperDash.Essentials.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
using PepperDash.Essentials.AppServer;
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class TransportExtensions
    {
        public static void LinkActions(this ITransport dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.AddAction(prefix + "play", new PressAndHoldAction(dev.Play));
            controller.AddAction(prefix + "pause", new PressAndHoldAction(dev.Pause));
            controller.AddAction(prefix + "stop", new PressAndHoldAction(dev.Stop));
            controller.AddAction(prefix + "prevTrack", new PressAndHoldAction(dev.ChapPlus));
            controller.AddAction(prefix + "nextTrack", new PressAndHoldAction(dev.ChapMinus));
            controller.AddAction(prefix + "rewind", new PressAndHoldAction(dev.Rewind));
            controller.AddAction(prefix + "ffwd", new PressAndHoldAction(dev.FFwd));
            controller.AddAction(prefix + "record", new PressAndHoldAction(dev.Record));
        }

        public static void UnlinkActions(this ITransport dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed) dev).Key);

            controller.RemoveAction(prefix + "play");
            controller.RemoveAction(prefix + "pause");
            controller.RemoveAction(prefix + "stop");
            controller.RemoveAction(prefix + "prevTrack");
            controller.RemoveAction(prefix + "nextTrack");
            controller.RemoveAction(prefix + "rewind");
            controller.RemoveAction(prefix + "ffwd");
            controller.RemoveAction(prefix + "record");
        }
    }
}