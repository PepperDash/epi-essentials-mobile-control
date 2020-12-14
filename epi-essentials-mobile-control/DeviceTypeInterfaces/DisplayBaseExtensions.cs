using System;
using System.Linq;
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

            foreach (var inputPort in display.InputPorts)
            {
                var input = inputPort;
                //path should be /device/deviceKey/inputName
                var path = String.Format("{0}{1}", prefix, input.Key);
                
                controller.AddAction(path, new Action(()=> display.ExecuteSwitch(input.Selector)));
            }

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

            foreach (var inputPort in display.InputPorts)
            {
                var input = inputPort;
                //path should be /device/deviceKey/inputName
                var path = String.Format("{0}{1}", prefix, input.Key);

                controller.RemoveAction(path);
            }
        }
    }
}