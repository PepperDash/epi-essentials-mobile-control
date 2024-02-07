using System;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Shades;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class IShadesOpenCloseStopMessenger : MessengerBase
    {
        protected IShadesOpenCloseStop Device { get; private set; }

        public IShadesOpenCloseStopMessenger(string key, IShadesOpenCloseStop device, string messagePath)
            : base(key, messagePath, device as Device)
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

            appServerController.AddAction(string.Format("{0}/fullStatus", MessagePath), (id, content) => SendFullStatus());

            appServerController.AddAction(string.Format("{0}/shadeUp", MessagePath), (id, content) =>
                {

                    Device.Open();

                });

            appServerController.AddAction(string.Format("{0}/shadeDown", MessagePath), (id, content) =>
                {

                    Device.Close();

                });

            var stopDevice = Device;
            if (stopDevice != null)
            {
                appServerController.AddAction(string.Format("{0}/stopOrPreset", MessagePath), (id, content) =>
                {
                    stopDevice.Stop();
                });
            }

            var feedbackDevice = Device as IShadesOpenClosedFeedback;
            if (feedbackDevice != null)
            {
                feedbackDevice.ShadeIsOpenFeedback.OutputChange += new EventHandler<Core.FeedbackEventArgs>(ShadeIsOpenFeedback_OutputChange);
                feedbackDevice.ShadeIsClosedFeedback.OutputChange += new EventHandler<Core.FeedbackEventArgs>(ShadeIsClosedFeedback_OutputChange);
            }
        }

        void ShadeIsOpenFeedback_OutputChange(object sender, Core.FeedbackEventArgs e)
        {
            var state = new ShadeBaseStateMessage();

            state.IsOpen = e.BoolValue;

            PostStatusMessage(state);
        }

        void ShadeIsClosedFeedback_OutputChange(object sender, Core.FeedbackEventArgs e)
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