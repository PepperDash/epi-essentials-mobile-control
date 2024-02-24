using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public static class TransportExtensions
    {
        public static void LinkActions(this ITransport dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

            controller.AddAction(prefix + "play", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Play(b)));
            controller.AddAction(prefix + "pause", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Pause(b)));
            controller.AddAction(prefix + "stop", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Stop(b)));
            controller.AddAction(prefix + "prevTrack", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.ChapPlus(b)));
            controller.AddAction(prefix + "nextTrack", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.ChapMinus(b)));
            controller.AddAction(prefix + "rewind", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Rewind(b)));
            controller.AddAction(prefix + "ffwd", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.FFwd(b)));
            controller.AddAction(prefix + "record", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => dev.Record(b)));
        }

        public static void UnlinkActions(this ITransport dev, IMobileControl3 controller)
        {
            var prefix = string.Format(@"/device/{0}/", ((IKeyed)dev).Key);

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