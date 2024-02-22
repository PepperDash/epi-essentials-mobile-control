﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Common.TouchPanel;

namespace PepperDash.Essentials.Touchpanel
{
    public class ITswAppControlMessenger : MessengerBase
    {
        private ITswAppControl _appControl;

        public ITswAppControlMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public ITswAppControlMessenger(string key, string messagePath, Device device) : base(key, messagePath, device)
        {
            _appControl = device as ITswAppControl;
        }

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            if (_appControl == null)
            {
                Debug.Console(0, this, $"{_device.Key} does not implement ITswAppControl");
                return;
            }

            appServerController.AddAction($"{MessagePath}/fullStatus", (id, context) => SendFullStatus());

            appServerController.AddAction($"{MessagePath}/openApp", (id, context) => _appControl.OpenApp());

            appServerController.AddAction($"{MessagePath}/closeApp", (id, context) => _appControl.CloseOpenApp());

            appServerController.AddAction($"{MessagePath}/hideApp", (id, context) => _appControl.HideOpenApp());

            _appControl.AppPackageFeedback.OutputChange += (s, a) =>
            {
                var message = new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(new
                    {
                        AppPackage = a.StringValue
                    })
                };

                appServerController.SendMessageObject(message);
            };

            _appControl.AppOpenFeedback.OutputChange += (s, a) => {
                var message = new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(new
                    {
                        AppOpen = a.BoolValue
                    })
                };

                appServerController.SendMessageObject(message);

            };
        }

        private void SendFullStatus()
        {
            var message = new TswAppStateMessage
            {
                AppOpen = _appControl.AppOpenFeedback.BoolValue
                AppPackage = _appControl.AppPackageFeedback.StringValue
            };

            PostStatusMessage(message);
        }
    }

    public class TswAppStateMessage:DeviceStateMessageBase
    {
        [JsonProperty("appOpen", NullValueHandling = NullValueHandling.Ignore)]
        public bool? AppOpen { get; set; }

        [JsonProperty("appPackage", NullValueHandling = NullValueHandling.Ignore)]
        public string AppPackage { get; set; }
    }
}
