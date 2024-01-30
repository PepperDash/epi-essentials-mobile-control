using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core.Shades;

using Newtonsoft.Json;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ShadeBaseMessenger : MessengerBase
    {
        protected ShadeBase Device { get; private set; }

        public ShadeBaseMessenger(string key, ShadeBase device, string messagePath)
            : base(key, messagePath, device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            Device = device;
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            base.CustomRegisterWithAppServer(appServerController);

            appServerController.AddAction(string.Format("{0}/fullStatus", MessagePath), new Action(SendFullStatus));

            appServerController.AddAction(string.Format("{0}/shadeUp", MessagePath), new Action( () =>
                {

                    Device.Open();

                }));

            appServerController.AddAction(string.Format("{0}/shadeDown", MessagePath), new Action(() =>
                {

                    Device.Close();

                }));

            var stopDevice = Device as IShadesOpenCloseStop;
            if (stopDevice != null)
            {
                appServerController.AddAction(string.Format("{0}/stopOrPreset", MessagePath), new Action(() =>
                {
                    stopDevice.Stop();
                }));
            }

            var feedbackDevice = Device as IShadesOpenClosedFeedback;
            if (feedbackDevice != null)
            {
                feedbackDevice.ShadeIsOpenFeedback.OutputChange += new EventHandler<PepperDash.Essentials.Core.FeedbackEventArgs>(ShadeIsOpenFeedback_OutputChange);
                feedbackDevice.ShadeIsClosedFeedback.OutputChange += new EventHandler<PepperDash.Essentials.Core.FeedbackEventArgs>(ShadeIsClosedFeedback_OutputChange);
            }
        }

        void ShadeIsOpenFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            var state = new ShadeBaseStateMessage();

            state.IsOpen = e.BoolValue;

            PostStatusMessage(state);
        }

        void ShadeIsClosedFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            var state = new ShadeBaseStateMessage();

            state.IsClosed = e.BoolValue;

            PostStatusMessage(state);
        }


        private void SendFullStatus()
        {
            var state = new ShadeBaseStateMessage();

            var feedbackDevice = Device as IShadesOpenClosedFeedback;
            if (feedbackDevice != null)
            {
                state.IsOpen = feedbackDevice.ShadeIsOpenFeedback.BoolValue;
                state.IsClosed = feedbackDevice.ShadeIsClosedFeedback.BoolValue;
            }

            PostStatusMessage(state);
        }
    }

    public class ShadeBaseStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("middleButtonLabel", NullValueHandling = NullValueHandling.Ignore)]
        public string MiddleButtonLabel { get; set; }

        [JsonProperty("isOpen", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsOpen { get; set; }

        [JsonProperty("isClosed", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsClosed { get; set; }
    }
}