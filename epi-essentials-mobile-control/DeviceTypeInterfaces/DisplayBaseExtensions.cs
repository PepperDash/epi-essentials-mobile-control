using System;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Room.MobileControl
{
    public static class DisplayBaseExtensions
    {
        public static void LinkActions(this DisplayBase display, MobileControlSystemController controller)
        {
            var prefix = String.Format(@"/device/{0}/", display.Key);

            controller.AddAction(prefix + "powerOn", new Action(display.PowerOn));
            controller.AddAction(prefix + "powerOff", new Action(display.PowerOff));
            controller.AddAction(prefix + "powerToggle", new Action(display.PowerToggle));

            controller.AddAction(prefix + "inputSelect", new Action<string>((s) =>
            {
                var inputPort = display.InputPorts.FirstOrDefault(i => i.Key == s);

                if (inputPort == null)
                {
                    Debug.Console(1, "No input named {0} found for device {1}", s, display.Key);
                    return;
                }

                display.ExecuteSwitch(inputPort.Selector);
            }));

            controller.AddAction(prefix + "inputs", new Action(() =>
            {
                var inputsList = display.InputPorts.Select(p => p.Key).ToList();

                var messageObject = new
                {
                    type = prefix + "inputs",
                    content = new
                    {
                        inputKeys = inputsList,
                    }
                };

                controller.SendMessageObjectToServer(messageObject);
            }));
        }

        public static void UnlinkActions(this DisplayBase display, MobileControlSystemController controller)
        {
            var prefix = String.Format(@"/device/{0}/", display.Key);

            controller.RemoveAction(prefix + "powerOn");
            controller.RemoveAction(prefix + "powerOff");
            controller.RemoveAction(prefix + "powerToggle");
            controller.RemoveAction(prefix + "inputs");
            controller.RemoveAction(prefix + "inputSelect");
        }
    }
}