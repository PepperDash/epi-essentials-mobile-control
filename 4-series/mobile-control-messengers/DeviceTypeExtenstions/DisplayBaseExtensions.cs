using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using DisplayBase = PepperDash.Essentials.Devices.Common.Displays.DisplayBase;

namespace PepperDash.Essentials.Room.MobileControl
{
    public static class DisplayBaseExtensions
    {
        public static void LinkActions(this DisplayBase display, IMobileControl3 controller)
        {
            var prefix = String.Format(@"/device/{0}/", display.Key);

            controller.AddAction(prefix + "powerOn", (id, content) => display.PowerOn());
            controller.AddAction(prefix + "powerOff", (id, content) => display.PowerOff());
            controller.AddAction(prefix + "powerToggle", (id, content) => display.PowerToggle());

            controller.AddAction(prefix + "inputSelect", (id, content) =>
            {
                var s = content.ToObject<MobileControlSimpleContent<string>>();

                var inputPort = display.InputPorts.FirstOrDefault(i => i.Key == s.Value);

                if (inputPort == null)
                {
                    Debug.Console(1, "No input named {0} found for device {1}", s, display.Key);
                    return;
                }

                display.ExecuteSwitch(inputPort.Selector);
            });

            controller.AddAction(prefix + "inputs", (id, content) =>
            {
                var inputsList = display.InputPorts.Select(p => p.Key).ToList();

                var messageObject = new MobileControlMessage
                {
                    Type = prefix + "inputs",
                    Content = JToken.FromObject(new
                    {
                        inputKeys = inputsList,
                    })
                };

                controller.SendMessageObject(messageObject);
            });
        }

        public static void UnlinkActions(this DisplayBase display, IMobileControl3 controller)
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
