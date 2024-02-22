using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PepperDash.Essentials.Touchpanel
{
    public class ITswZoomControlMessenger : MessengerBase
    {
        private ITswZoomControl _zoomControl;

        public ITswZoomControlMessenger(string key, string messagePath, Device device) : base(key, messagePath, device)
        {
            _zoomControl = device as ITswZoomControl;
        }

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            if (_zoomControl == null)
            {
                Debug.Console(0, this, $"{_device.Key} does not implement ITswZoomControl");
                return;
            }

            appServerController.AddAction($"{MessagePath}/fullStatus", (id, context) => SendFullStatus());

            
            appServerController.AddAction($"{MessagePath}/endCall", (id, context) => _zoomControl.EndZoomCall());

            _zoomControl.ZoomIncomingCallFeedback.OutputChange += (s, a) =>
            {
                var message = new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(new
                    {
                        InCall = a.BoolValue,
                    })
                };

                appServerController.SendMessageObject(message);
            };


            _zoomControl.ZoomInCallFeedback.OutputChange += (s, a) =>
            {
                var message = new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(
                        new
                        {
                            IncomingCall = a.BoolValue,
                        })
                };

                appServerController.SendMessageObject(message);
            };
        }

        private void SendFullStatus()
        {
            var message = new TswZoomStateMessage
            {
                InCall = _zoomControl?.ZoomInCallFeedback.BoolValue,
                IncomingCall = _zoomControl?.ZoomIncomingCallFeedback.BoolValue
            };

            PostStatusMessage(message);
        }
    }

    public class TswZoomStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("inCall", NullValueHandling = NullValueHandling.Ignore)]
        public bool? InCall { get; set; }

        [JsonProperty("IncomingCall", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IncomingCall { get; set; }
    }
}
